# Detective Agent (.NET)

A foundational LLM agent built from first principles for release risk assessment. This implementation follows a transparent, observable, and extensible architecture.

## Current Status: Steps 1-5 Complete ✅

**Step 1: Basic Conversation** ✅ - Conversation with LLM, history, and persistence  
**Step 2: Observability** ✅ - OpenTelemetry tracing with spans and trace export  
**Step 3: Context Window Management** ✅ - Token estimation and automatic truncation  
**Step 4: Retry Mechanism** ✅ - Exponential backoff with Polly for transient failures  
**Step 5: System Prompt Engineering** ✅ - Enhanced Detective Agent prompts with multiple configurations

## What's Implemented

### Core Components
- **Message & Conversation Models**: Immutable C# records for clean data modeling
- **Provider Abstraction**: `ILlmProvider` interface enabling multi-provider support
- **Anthropic Provider**: Full implementation of Claude 3.5 Sonnet integration
- **Ollama Provider**: Local LLM support via Ollama API
- **Agent Core**: Orchestrates conversation with LLM, maintains state
- **Context Window Manager**: Automatic truncation when approaching token limits
- **File System Storage**: Persistent conversation storage as JSON
- **OpenTelemetry Observability**: Full tracing with spans exported to filesystem
- **CLI Application**: Interactive terminal interface for testing

### Features
- ✅ Back-and-forth conversation with Claude or Ollama
- ✅ Conversation history maintained in memory
- ✅ Persistent storage to filesystem with trace IDs
- ✅ Load and continue previous conversations
- ✅ Multiple conversation management
- ✅ Provider abstraction (Anthropic, Ollama)
- ✅ **Context window management** with automatic truncation
- ✅ **OpenTelemetry tracing** - all operations captured as spans
- ✅ **Trace export to JSON files** for analysis
- ✅ **Token counting and budget allocation**
- ✅ Context utilization tracking in traces and metadata
- ✅ **Retry mechanism with exponential backoff** using Polly
- ✅ **Rate limit handling** (429) and transient error recovery
- ✅ **Enhanced system prompts** for Detective Agent personality
- ✅ **Configurable prompts** via appsettings.json (Production, Development, Audit, Research modes)
- ✅ Comprehensive error handling
- ✅ **32+ automated xUnit tests passing** (includes SystemPromptTests)

## Quick Start

See [quick-start.md](quick-start.md) for detailed setup and testing instructions.

### 🔐 Important: API Key Security

**Before running the application**, you need to configure your Anthropic API key securely:

**Recommended:** Use `appsettings.Development.json` (already created with your key)
```bash
# Your key is already in appsettings.Development.json
# This file is gitignored and won't be committed
dotnet run --project samples/DetectiveAgent.Cli
```

See **[API-KEYS.md](API-KEYS.md)** for complete security guide including:
- ✅ How to keep API keys secure
- ✅ Multiple configuration options
- ✅ What's protected by .gitignore
- ✅ Production deployment strategies

**Quick Test:**
```bash
# 1. Build
dotnet build

# 2. Run tests
dotnet test

# 3. Run CLI (your API key is already configured)
dotnet run --project samples/DetectiveAgent.Cli
```

## Project Structure

```
DetectiveAgent/
├── src/DetectiveAgent/              # Core library
│   ├── Core/
│   │   ├── Agent.cs                 # Main agent orchestrator
│   │   ├── Conversation.cs          # Conversation model
│   │   └── Message.cs               # Message model
│   ├── Providers/
│   │   ├── ILlmProvider.cs          # Provider interface
│   │   ├── AnthropicProvider.cs     # Claude implementation
│   │   ├── OllamaProvider.cs        # Ollama implementation
│   │   └── ProviderExceptions.cs    # Custom exceptions
│   ├── Context/
│   │   └── ContextWindowManager.cs  # Context window management
│   ├── Retry/
│   │   ├── PollyRetryPolicy.cs      # Retry policy implementation
│   │   └── RetryConfiguration.cs    # Retry settings
│   ├── Observability/
│   │   ├── AgentActivitySource.cs   # OpenTelemetry source
│   │   └── FileSystemTraceExporter.cs # Trace export
│   └── Storage/
│       ├── IConversationStore.cs    # Storage interface
│       └── FileSystemConversationStore.cs
├── samples/DetectiveAgent.Cli/      # CLI application
│   ├── Program.cs                   # Main CLI logic
│   ├── appsettings.json             # Production configuration
│   ├── appsettings.Development.json # Development prompt (friendly)
│   ├── appsettings.Audit.json       # Audit mode (strict)
│   └── appsettings.Research.json    # Research mode (exploratory)
├── tests/DetectiveAgent.Tests/      # Unit tests
│   └── Core/AgentTests.cs           # Agent tests
├── memory/                          # Design documents
│   ├── DESIGN.md                    # System design spec
│   ├── PLAN.md                      # .NET implementation plan
│   ├── STEPS.md                     # Implementation steps
│   └── releases.json                # Test data for future tools
├── quick-start.md                   # Quick start guide
└── README.md                        # This file
```

## Architecture Highlights

### Provider Abstraction
The `ILlmProvider` interface allows swapping between different LLM providers:
- Currently implemented: Anthropic Claude
- Future: OpenRouter, Ollama, Azure OpenAI

### Conversation Persistence
Conversations are automatically saved to `data/conversations/` as JSON files, allowing:
- Session resumption
- Conversation history analysis
- Debugging and auditing

### Dependency Injection
Uses Microsoft.Extensions.DependencyInjection for clean service composition:
```csharp
services.AddSingleton<ILlmProvider, AnthropicProvider>();
services.AddSingleton<IConversationStore, FileSystemConversationStore>();
services.AddSingleton<Agent>();
```

## Design Philosophy

This agent is built with:
1. **Transparency Over Magic**: Every component is explicit and understandable
2. **Observability First**: Complete visibility into agent behavior (Step 2)
3. **Provider Agnostic**: Support multiple LLMs through clean interfaces
4. **Resilient**: Graceful error handling and recovery (Step 4)
5. **Extensible**: Clean interfaces for future capabilities

## Next Steps

### Step 6: Tool Abstraction (Next)
- Tool calling framework
- Get Release Summary tool
- File Risk Report tool
- Integration with releases.json

### Step 7: Evaluation System
- Automated behavior validation
- Tool usage evaluation
- Decision quality assessment
- Regression tracking

## Technology Stack

- **.NET 9.0**: Modern C# features (records, nullable types, async/await)
- **Microsoft.Extensions.Http**: HTTP client factory and resilience
- **Microsoft.Extensions.DependencyInjection**: Service composition
- **Microsoft.Extensions.Configuration**: Configuration management
- **System.Text.Json**: JSON serialization
- **xUnit**: Testing framework
- **Moq**: Mocking library

## Testing

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test
dotnet test --filter "FullyQualifiedName~SendMessageAsync_ShouldReturnAssistantMessage"
```

Current test coverage:
- Agent initialization and conversation management
- Message sending and response handling
- Conversation history maintenance
- Persistence (save/load) with trace IDs
- Multiple conversation handling
- OpenTelemetry activity creation and tagging
- Trace export to filesystem
- Context window management and truncation
- Token estimation and budget allocation
- Context utilization tracking

## Configuration

Configure via `appsettings.json`:
```json
{
  "Agent": {
    "SystemPrompt": "You are a Detective Agent specializing in software release risk assessment...",
    "Temperature": 0.7,
    "MaxTokens": 2048,
    "DefaultProvider": "Ollama"
  },
  "Providers": {
    "Anthropic": {
      "ApiKey": "your-api-key-here",
      "Model": "claude-3-haiku-20240307"
    },
    "Ollama": {
      "Model": "qwen2.5:0.5b",
      "BaseUrl": "http://localhost:11434"
    }
  },
  "Storage": {
    "ConversationsPath": "./data/conversations",
    "TracesPath": "./data/traces"
  },
  "Retry": {
    "MaxAttempts": 3,
    "InitialDelayMs": 1000,
    "MaxDelayMs": 60000,
    "BackoffFactor": 2.0,
    "UseJitter": true
  }
}
```

### System Prompt Configurations

The Detective Agent includes multiple prompt configurations for different use cases:

- **Production** (`appsettings.json`): Direct, thorough risk assessment specialist
- **Development** (`appsettings.Development.json`): Friendly, collaborative feedback mode
- **Audit** (`appsettings.Audit.json`): Strict, conservative risk classification
- **Research** (`appsettings.Research.json`): Exploratory pattern analysis mode

See [STEP5-PROMPT-ENGINEERING.md](STEP5-PROMPT-ENGINEERING.md) for detailed prompt engineering guide.

## Contributing

This is an educational project following the Detective Agent design specification. The implementation follows a strict step-by-step approach to demonstrate how to build production-quality agents from first principles.

## License

Educational project - see design documentation for details.

## Resources

- [Design Specification](memory/DESIGN.md) - System architecture and design decisions
- [Implementation Plan](memory/PLAN.md) - .NET-specific implementation details
- [Implementation Steps](memory/STEPS.md) - Step-by-step guide
- [Quick Start Guide](quick-start.md) - Manual testing instructions
- [Step 4: Retry Implementation](STEP4-RETRY-IMPLEMENTATION.md) - Retry mechanism details
- [Step 5: Prompt Engineering](STEP5-PROMPT-ENGINEERING.md) - System prompt guide
