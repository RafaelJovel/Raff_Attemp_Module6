using DetectiveAgent.Core;
using DetectiveAgent.Providers;
using DetectiveAgent.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

// Register services
var apiKey = configuration["Providers:Anthropic:ApiKey"] 
    ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException(
        "Anthropic API key not found. Set it in appsettings.json or ANTHROPIC_API_KEY environment variable.");

var model = configuration["Providers:Anthropic:Model"] ?? "claude-3-5-sonnet-20241022";
var conversationsPath = configuration["Storage:ConversationsPath"] ?? "./data/conversations";
var systemPrompt = configuration["Agent:SystemPrompt"] ?? "You are a helpful AI assistant.";
var temperature = float.Parse(configuration["Agent:Temperature"] ?? "0.7");
var maxTokens = int.Parse(configuration["Agent:MaxTokens"] ?? "4096");

// Register HttpClient for Anthropic provider
builder.Services.AddHttpClient<ILlmProvider, AnthropicProvider>((sp, client) =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
})
.Services.AddSingleton<ILlmProvider>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AnthropicProvider));
    var logger = sp.GetRequiredService<ILogger<AnthropicProvider>>();
    return new AnthropicProvider(httpClient, logger, apiKey, model);
});

// Register conversation store
builder.Services.AddSingleton<IConversationStore>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileSystemConversationStore>>();
    return new FileSystemConversationStore(logger, conversationsPath);
});

// Register agent
builder.Services.AddSingleton(sp =>
{
    var provider = sp.GetRequiredService<ILlmProvider>();
    var store = sp.GetRequiredService<IConversationStore>();
    var logger = sp.GetRequiredService<ILogger<Agent>>();
    return new Agent(provider, store, logger, systemPrompt, temperature, maxTokens);
});

var host = builder.Build();

// Run the CLI
var agent = host.Services.GetRequiredService<Agent>();
var conversationStore = host.Services.GetRequiredService<IConversationStore>();

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
