# Detective Agent Implementation Plan (.NET)

## Overview
.NET implementation of the Detective Agent. See [DESIGN.md](DESIGN.md) for more about **what** the agent does and **why** design decisions were made.

This document covers **how** to build the agent in .NET - specific packages, project structure, testing approach, and implementation details.  

## Implementation Goals
- Clear, readable C# code that shows exactly what's happening
- Multi-provider support (Anthropic, OpenRouter, Ollama, etc)
- OpenTelemetry observability
- Context window management
- Retry mechanism with exponential backoff
- Tool calling foundation
- Interaction persistence
- Basic reasoning and evaluations

## Implementation Constitution
- Clear, readable C# code that shows exactly what's happening
- Use interfaces (not abstract classes) for abstractions
- Follow modern C# conventions (nullable reference types, records for DTOs, async/await)
- Use minimal APIs or clean architecture patterns
- Place unit tests in separate test projects following .NET conventions
- Test projects have `.Tests` suffix (e.g., `DetectiveAgent.Tests`)
- The `tests` folder (or separate test project) should contain both unit and integration tests
- Use `dotnet` CLI for all project operations
- Use NuGet for package management
- Follow .NET naming conventions (PascalCase for public members, camelCase for private)
- Leverage built-in dependency injection (Microsoft.Extensions.DependencyInjection)
- Use System.Text.Json for JSON serialization
- Prefer record types for immutable data models

## Implementation Steps
The recommended order of implementation is defined in [STEPS.md](STEPS.md). The phases of implementation defined later in this document align with these progression of steps.

## Technology Stack
- **.NET 8.0 or later** with async/await
- **dotnet CLI** for project management and package installation
- **HttpClient** (with IHttpClientFactory) for HTTP requests
- **OpenTelemetry .NET SDK** for traces and metrics
  - OpenTelemetry.Api
  - OpenTelemetry.Exporter.Console
  - OpenTelemetry.Exporter.OpenTelemetryProtocol
  - OpenTelemetry.Instrumentation.Http
- **System.Text.Json** for JSON serialization and validation
- **FluentValidation** (optional) for advanced validation scenarios
- **xUnit** for testing framework
- **Moq** or **NSubstitute** for mocking
- **WireMock.Net** for HTTP mocking in integration tests
- **Microsoft.Extensions.Configuration** for configuration management
- **Microsoft.Extensions.Logging** for structured logging
- **Microsoft.Extensions.DependencyInjection** for dependency injection
- **Microsoft.Extensions.Http** for IHttpClientFactory
- **Microsoft.Extensions.Http.Resilience** for retry policies (or Polly)

## Project Structure

```
DetectiveAgent/
├── src/
│   └── DetectiveAgent/
│       ├── DetectiveAgent.csproj
│       ├── Core/
│       │   ├── Agent.cs
│       │   ├── Conversation.cs
│       │   └── Message.cs
│       ├── Providers/
│       │   ├── ILlmProvider.cs
│       │   ├── AnthropicProvider.cs
│       │   ├── OpenRouterProvider.cs
│       │   └── OllamaProvider.cs
│       ├── Tools/
│       │   ├── IToolRegistry.cs
│       │   ├── ToolDefinition.cs
│       │   ├── ToolExecutor.cs
│       │   └── Implementations/
│       │       ├── GetReleaseSummaryTool.cs
│       │       └── FileRiskReportTool.cs
│       ├── Retry/
│       │   ├── RetryPolicy.cs
│       │   └── ExponentialBackoff.cs
│       ├── Context/
│       │   ├── ContextWindowManager.cs
│       │   └── TokenEstimator.cs
│       ├── Observability/
│       │   ├── Tracing/
│       │   │   ├── AgentActivitySource.cs
│       │   │   └── TraceExporter.cs
│       │   └── Metrics/
│       │       └── AgentMetrics.cs
│       ├── Storage/
│       │   ├── IConversationStore.cs
│       │   └── FileSystemConversationStore.cs
│       └── Configuration/
│           ├── AgentConfiguration.cs
│           └── ProviderConfiguration.cs
├── tests/
│   ├── DetectiveAgent.Tests/
│   │   ├── DetectiveAgent.Tests.csproj
│   │   ├── Core/
│   │   │   ├── AgentTests.cs
│   │   │   └── ConversationTests.cs
│   │   ├── Providers/
│   │   │   └── AnthropicProviderTests.cs
│   │   ├── Tools/
│   │   │   └── ToolExecutorTests.cs
│   │   └── Integration/
│   │       └── EndToEndTests.cs
│   └── DetectiveAgent.Evaluations/
│       ├── DetectiveAgent.Evaluations.csproj
│       ├── EvaluationRunner.cs
│       ├── Scenarios/
│       │   ├── HighRiskScenario.cs
│       │   ├── MediumRiskScenario.cs
│       │   └── ErrorHandlingScenario.cs
│       └── Evaluators/
│           ├── ToolUsageEvaluator.cs
│           └── DecisionQualityEvaluator.cs
├── samples/
│   └── DetectiveAgent.Cli/
│       ├── DetectiveAgent.Cli.csproj
│       ├── Program.cs
│       └── appsettings.json
├── data/
│   ├── conversations/
│   └── traces/
├── DetectiveAgent.sln
└── README.md
```

## Data Models

### Using C# Records for Immutability

```csharp
// Message.cs
public record Message(
    MessageRole Role,
    string Content,
    DateTimeOffset Timestamp,
    Dictionary<string, object>? Metadata = null
);

public enum MessageRole
{
    User,
    Assistant,
    System
}

// Conversation.cs
public record Conversation
{
    public required string Id { get; init; }
    public required string SystemPrompt { get; init; }
    public required List<Message> Messages { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

// ToolDefinition.cs
public record ToolDefinition(
    string Name,
    string Description,
    JsonDocument ParametersSchema,
    Func<JsonDocument, Task<ToolResult>> Handler
);

// ToolCall.cs
public record ToolCall(
    string Id,
    string Name,
    JsonDocument Arguments,
    DateTimeOffset Timestamp
);

// ToolResult.cs
public record ToolResult(
    string ToolCallId,
    string Content,
    bool Success,
    DateTimeOffset Timestamp,
    Dictionary<string, object>? Metadata = null
);
```

## Provider Abstraction

```csharp
public interface ILlmProvider
{
    Task<Message> CompleteAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default,
        float? temperature = null,
        int? maxTokens = null,
        IReadOnlyList<ToolDefinition>? tools = null);

    Task<int> EstimateTokensAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);

    ProviderCapabilities GetCapabilities();
}

public record ProviderCapabilities(
    bool SupportsTools,
    bool SupportsVision,
    bool SupportsStreaming,
    int MaxContextTokens
);
```

## Configuration Management

Use `appsettings.json` for configuration:

```json
{
  "Agent": {
    "SystemPrompt": "You are a detective agent...",
    "DefaultProvider": "Anthropic",
    "MaxTokens": 4096,
    "Temperature": 0.7
  },
  "Providers": {
    "Anthropic": {
      "ApiKey": "env:ANTHROPIC_API_KEY",
      "Model": "claude-3-5-sonnet-20241022",
      "BaseUrl": "https://api.anthropic.com"
    },
    "OpenRouter": {
      "ApiKey": "env:OPENROUTER_API_KEY",
      "Model": "anthropic/claude-3.5-sonnet",
      "BaseUrl": "https://openrouter.ai/api/v1"
    }
  },
  "Retry": {
    "MaxAttempts": 3,
    "InitialDelayMs": 1000,
    "MaxDelayMs": 60000,
    "BackoffFactor": 2.0
  },
  "Storage": {
    "ConversationsPath": "./data/conversations",
    "TracesPath": "./data/traces"
  }
}
```

## Dependency Injection Setup

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

// Logging
builder.Logging.AddConsole();

// HTTP Clients with Resilience
builder.Services.AddHttpClient<ILlmProvider, AnthropicProvider>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
    });

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("DetectiveAgent")
        .AddHttpClientInstrumentation()
        .AddConsoleExporter()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("DetectiveAgent")
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Agent Services
builder.Services.AddSingleton<IConversationStore, FileSystemConversationStore>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<Agent>();
builder.Services.AddSingleton<ActivitySource>(
    sp => new ActivitySource("DetectiveAgent"));

var host = builder.Build();
await host.RunAsync();
```

## Error Handling

Use custom exception types:

```csharp
public class LlmProviderException : Exception
{
    public LlmProviderException(string message) : base(message) { }
    public LlmProviderException(string message, Exception inner) 
        : base(message, inner) { }
}

public class RateLimitException : LlmProviderException
{
    public TimeSpan? RetryAfter { get; init; }
    
    public RateLimitException(string message, TimeSpan? retryAfter = null) 
        : base(message)
    {
        RetryAfter = retryAfter;
    }
}

public class AuthenticationException : LlmProviderException
{
    public AuthenticationException(string message) : base(message) { }
}

public class ToolExecutionException : Exception
{
    public string ToolName { get; init; }
    
    public ToolExecutionException(string toolName, string message) 
        : base(message)
    {
        ToolName = toolName;
    }
}
```

## Observability with OpenTelemetry

```csharp
public class Agent
{
    private readonly ActivitySource _activitySource;
    
    public Agent(ActivitySource activitySource, ...)
    {
        _activitySource = activitySource;
    }
    
    public async Task<Message> SendMessageAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity(
            "Agent.SendMessage",
            ActivityKind.Internal);
            
        activity?.SetTag("message.length", content.Length);
        activity?.SetTag("conversation.id", _conversation.Id);
        
        try
        {
            // Implementation
            var response = await _provider.CompleteAsync(...);
            
            activity?.SetTag("response.tokens", 
                response.Metadata?["tokens"]);
            
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

## Testing Approach

### Unit Tests with xUnit and Moq

```csharp
public class AgentTests
{
    private readonly Mock<ILlmProvider> _mockProvider;
    private readonly Mock<IConversationStore> _mockStore;
    private readonly Agent _agent;
    
    public AgentTests()
    {
        _mockProvider = new Mock<ILlmProvider>();
        _mockStore = new Mock<IConversationStore>();
        _agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            new ActivitySource("Test"));
    }
    
    [Fact]
    public async Task SendMessage_ShouldReturnResponse()
    {
        // Arrange
        var expectedResponse = new Message(
            MessageRole.Assistant,
            "Hello!",
            DateTimeOffset.UtcNow);
            
        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                null, null, null))
            .ReturnsAsync(expectedResponse);
        
        // Act
        var result = await _agent.SendMessageAsync("Hi");
        
        // Assert
        Assert.Equal("Hello!", result.Content);
    }
}
```

### Integration Tests with WireMock.Net

```csharp
public class AnthropicProviderIntegrationTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly AnthropicProvider _provider;
    
    public AnthropicProviderIntegrationTests()
    {
        _server = WireMockServer.Start();
        
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_server.Urls[0])
        };
        
        _provider = new AnthropicProvider(
            httpClient,
            Options.Create(new AnthropicConfiguration
            {
                ApiKey = "test-key",
                Model = "claude-3-5-sonnet-20241022"
            }));
    }
    
    [Fact]
    public async Task CompleteAsync_ShouldCallAnthropicApi()
    {
        // Arrange
        _server
            .Given(Request.Create()
                .WithPath("/v1/messages")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{
                    ""role"": ""assistant"",
                    ""content"": ""Hello!""
                }"));
        
        var messages = new List<Message>
        {
            new(MessageRole.User, "Hi", DateTimeOffset.UtcNow)
        };
        
        // Act
        var response = await _provider.CompleteAsync(messages);
        
        // Assert
        Assert.Equal("Hello!", response.Content);
    }
    
    public void Dispose() => _server.Dispose();
}
```

## CLI Application

```csharp
// Program.cs for CLI
using DetectiveAgent.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configure services (as shown above)
ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

// Run interactive CLI
var agent = host.Services.GetRequiredService<Agent>();

Console.WriteLine("Detective Agent CLI");
Console.WriteLine("Type 'exit' to quit\n");

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    
    if (string.IsNullOrEmpty(input) || input == "exit")
        break;
    
    try
    {
        var response = await agent.SendMessageAsync(input);
        Console.WriteLine($"Agent: {response.Content}\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}\n");
    }
}
```

## Package Installation Commands

```bash
# Create solution and projects
dotnet new sln -n DetectiveAgent
dotnet new classlib -n DetectiveAgent -o src/DetectiveAgent
dotnet new xunit -n DetectiveAgent.Tests -o tests/DetectiveAgent.Tests
dotnet new console -n DetectiveAgent.Cli -o samples/DetectiveAgent.Cli

# Add projects to solution
dotnet sln add src/DetectiveAgent/DetectiveAgent.csproj
dotnet sln add tests/DetectiveAgent.Tests/DetectiveAgent.Tests.csproj
dotnet sln add samples/DetectiveAgent.Cli/DetectiveAgent.Cli.csproj

# Add project references
dotnet add tests/DetectiveAgent.Tests reference src/DetectiveAgent
dotnet add samples/DetectiveAgent.Cli reference src/DetectiveAgent

# Install packages for main project
dotnet add src/DetectiveAgent package Microsoft.Extensions.Http
dotnet add src/DetectiveAgent package Microsoft.Extensions.DependencyInjection
dotnet add src/DetectiveAgent package Microsoft.Extensions.Configuration
dotnet add src/DetectiveAgent package Microsoft.Extensions.Configuration.Json
dotnet add src/DetectiveAgent package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add src/DetectiveAgent package Microsoft.Extensions.Logging
dotnet add src/DetectiveAgent package Microsoft.Extensions.Http.Resilience
dotnet add src/DetectiveAgent package OpenTelemetry
dotnet add src/DetectiveAgent package OpenTelemetry.Exporter.Console
dotnet add src/DetectiveAgent package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add src/DetectiveAgent package OpenTelemetry.Instrumentation.Http
dotnet add src/DetectiveAgent package OpenTelemetry.Extensions.Hosting
dotnet add src/DetectiveAgent package System.Text.Json

# Install packages for tests
dotnet add tests/DetectiveAgent.Tests package xunit
dotnet add tests/DetectiveAgent.Tests package xunit.runner.visualstudio
dotnet add tests/DetectiveAgent.Tests package Moq
dotnet add tests/DetectiveAgent.Tests package WireMock.Net
dotnet add tests/DetectiveAgent.Tests package Microsoft.NET.Test.Sdk

# Install packages for CLI
dotnet add samples/DetectiveAgent.Cli package Microsoft.Extensions.Hosting
```

<instructions_for_ai_assistant>
Read @DESIGN.md and @STEPS.md. Complete the rest of this document with implementation steps that align to these design principles. The design allows for flexibility in certain areas. When you have multiple options, ask the user what their preference is - do not make assumptions or fundamental design decisions on your own.
</instructions_for_ai_assistant>
