using System.Diagnostics;
using DetectiveAgent.Context;
using DetectiveAgent.Core;
using DetectiveAgent.Observability;
using DetectiveAgent.Providers;
using DetectiveAgent.Retry;
using DetectiveAgent.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)  // Load development overrides
    .AddEnvironmentVariables()
    .Build();

// Build host with dependency injection
var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Configure OpenTelemetry
var tracesPath = configuration["Storage:TracesPath"] ?? "./data/traces";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("DetectiveAgent", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource(AgentActivitySource.SourceName)
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddProcessor(new OpenTelemetry.SimpleActivityExportProcessor(
            new FileSystemTraceExporter(tracesPath))));

Console.WriteLine($"Tracing enabled. Traces will be saved to: {tracesPath}");

// Register services
var conversationsPath = configuration["Storage:ConversationsPath"] ?? "./data/conversations";
var systemPrompt = configuration["Agent:SystemPrompt"] ?? "You are a helpful AI assistant.";
var temperature = float.Parse(configuration["Agent:Temperature"] ?? "0.7");
var maxTokens = int.Parse(configuration["Agent:MaxTokens"] ?? "4096");
var defaultProvider = configuration["Agent:DefaultProvider"] ?? "Anthropic";

// Load retry configuration
var retryConfig = new RetryConfiguration();
configuration.GetSection("Retry").Bind(retryConfig);
Console.WriteLine($"Retry configuration: MaxAttempts={retryConfig.MaxAttempts}, InitialDelay={retryConfig.InitialDelayMs}ms");

// Helper method to create retry policy
static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(RetryConfiguration config, ILogger logger)
{
    return Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                       r.StatusCode == System.Net.HttpStatusCode.InternalServerError ||
                       r.StatusCode == System.Net.HttpStatusCode.BadGateway ||
                       r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                       r.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
        .WaitAndRetryAsync(
            config.MaxAttempts,
            retryAttempt =>
            {
                var delay = config.InitialDelayMs * Math.Pow(config.BackoffFactor, retryAttempt - 1);
                var cappedDelay = Math.Min(delay, config.MaxDelayMs);
                if (config.UseJitter)
                {
                    var jitter = Random.Shared.NextDouble() * 0.3;
                    cappedDelay = cappedDelay * (1 + jitter);
                }
                return TimeSpan.FromMilliseconds(cappedDelay);
            },
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                var statusCode = outcome.Result?.StatusCode.ToString() ?? "Exception";
                var exception = outcome.Exception?.Message ?? "None";
                logger.LogWarning(
                    "Retry attempt {RetryAttempt} after {DelayMs}ms. Status: {StatusCode}, Exception: {Exception}",
                    retryAttempt, timespan.TotalMilliseconds, statusCode, exception);

                // Add retry information to current activity for tracing
                var activity = Activity.Current;
                activity?.SetTag($"retry.attempt_{retryAttempt}.status", statusCode);
                activity?.SetTag($"retry.attempt_{retryAttempt}.delay_ms", timespan.TotalMilliseconds);
            });
}

// Register provider based on configuration
switch (defaultProvider.ToLowerInvariant())
{
    case "anthropic":
        var anthropicApiKey = configuration["Providers:Anthropic:ApiKey"] 
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException(
                "Anthropic API key not found. Set it in appsettings.json or ANTHROPIC_API_KEY environment variable.");
        
        var anthropicModel = configuration["Providers:Anthropic:Model"] ?? "claude-3-5-sonnet-20241022";
        var anthropicBaseUrl = configuration["Providers:Anthropic:BaseUrl"] ?? "https://api.anthropic.com";
        
        builder.Services.AddHttpClient(nameof(AnthropicProvider), client =>
        {
            client.BaseAddress = new Uri(anthropicBaseUrl);
        })
        .AddPolicyHandler((sp, req) => CreateRetryPolicy(retryConfig, 
            sp.GetRequiredService<ILogger<AnthropicProvider>>()));
        
        builder.Services.AddSingleton<ILlmProvider>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AnthropicProvider));
            var logger = sp.GetRequiredService<ILogger<AnthropicProvider>>();
            return new AnthropicProvider(httpClient, logger, anthropicApiKey, anthropicModel);
        });
        
        Console.WriteLine($"Using Anthropic provider with model: {anthropicModel}");
        break;
        
    case "ollama":
        var ollamaModel = configuration["Providers:Ollama:Model"] ?? "llama2";
        var ollamaBaseUrl = configuration["Providers:Ollama:BaseUrl"] ?? "http://localhost:11434";
        
        builder.Services.AddHttpClient(nameof(OllamaProvider), client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5); // Ollama can be slow on first load
        })
        .AddPolicyHandler((sp, req) => CreateRetryPolicy(retryConfig, 
            sp.GetRequiredService<ILogger<OllamaProvider>>()));
        
        builder.Services.AddSingleton<ILlmProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(OllamaProvider));
            var logger = sp.GetRequiredService<ILogger<OllamaProvider>>();
            return new OllamaProvider(httpClient, logger, ollamaModel);
        });
        
        Console.WriteLine($"Using Ollama provider with model: {ollamaModel} at {ollamaBaseUrl}");
        break;
        
    default:
        throw new InvalidOperationException($"Unknown provider: {defaultProvider}. Supported providers: Anthropic, Ollama");
}

// Register conversation store
builder.Services.AddSingleton<IConversationStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileSystemConversationStore>>();
    return new FileSystemConversationStore(logger, conversationsPath);
});

// Register context window manager
builder.Services.AddSingleton<ContextWindowManager>();

// Register agent
builder.Services.AddSingleton(sp =>
{
    var provider = sp.GetRequiredService<ILlmProvider>();
    var store = sp.GetRequiredService<IConversationStore>();
    var logger = sp.GetRequiredService<ILogger<Agent>>();
    var contextManager = sp.GetRequiredService<ContextWindowManager>();
    return new Agent(provider, store, logger, contextManager, systemPrompt, temperature, maxTokens);
});

var host = builder.Build();

// Start the host to ensure OpenTelemetry is initialized
await host.StartAsync();

// Run the CLI
var agent = host.Services.GetRequiredService<Agent>();
var conversationStore = host.Services.GetRequiredService<IConversationStore>();

// Create initial conversation AFTER OpenTelemetry is initialized
// This ensures the conversation gets a proper trace ID
agent.StartNewConversation(systemPrompt);

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("   Detective Agent CLI");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("Commands:");
Console.WriteLine("  'exit' or 'quit' - Exit the application");
Console.WriteLine("  'new' - Start a new conversation");
Console.WriteLine("  'list' - List all saved conversations");
Console.WriteLine("  'load <id>' - Load a conversation by ID");
Console.WriteLine("  'history' - Show current conversation history");
Console.WriteLine();
Console.WriteLine($"Current conversation: {agent.GetCurrentConversationId()}");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    // Handle commands
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    if (input.Equals("new", StringComparison.OrdinalIgnoreCase))
    {
        agent.StartNewConversation(systemPrompt);
        Console.WriteLine($"Started new conversation: {agent.GetCurrentConversationId()}");
        Console.WriteLine();
        continue;
    }

    if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
    {
        var conversations = await conversationStore.ListAsync();
        Console.WriteLine($"Found {conversations.Count} conversation(s):");
        foreach (var id in conversations)
        {
            Console.WriteLine($"  - {id}");
        }
        Console.WriteLine();
        continue;
    }

    if (input.StartsWith("load ", StringComparison.OrdinalIgnoreCase))
    {
        var conversationId = input.Substring(5).Trim();
        var loaded = await agent.LoadConversationAsync(conversationId);
        if (loaded)
        {
            Console.WriteLine($"Loaded conversation: {conversationId}");
            var history = agent.GetHistory();
            Console.WriteLine($"Conversation has {history.Count} message(s)");
        }
        else
        {
            Console.WriteLine($"Conversation not found: {conversationId}");
        }
        Console.WriteLine();
        continue;
    }

    if (input.Equals("history", StringComparison.OrdinalIgnoreCase))
    {
        var history = agent.GetHistory();
        Console.WriteLine($"Conversation history ({history.Count} messages):");
        Console.WriteLine("─────────────────────────────────────────────────────────");
        foreach (var msg in history)
        {
            var roleLabel = msg.Role == MessageRole.User ? "You" : "Agent";
            var preview = msg.Content.Length > 100 
                ? msg.Content.Substring(0, 100) + "..." 
                : msg.Content;
            Console.WriteLine($"[{msg.Timestamp:HH:mm:ss}] {roleLabel}: {preview}");
        }
        Console.WriteLine("─────────────────────────────────────────────────────────");
        Console.WriteLine();
        continue;
    }

    // Send message to agent
    try
    {
        var response = await agent.SendMessageAsync(input);
        Console.WriteLine($"Agent: {response.Content}");
        
        // Show token usage if available
        if (response.Metadata != null && 
            response.Metadata.ContainsKey("input_tokens") && 
            response.Metadata.ContainsKey("output_tokens"))
        {
            Console.WriteLine($"[Tokens: {response.Metadata["input_tokens"]} in, {response.Metadata["output_tokens"]} out]");
        }
        
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine();
    }
}

// Properly shutdown host to flush traces
await host.StopAsync();
await host.WaitForShutdownAsync();
