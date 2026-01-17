# Detective Agent (.NET)

A foundational LLM agent built from first principles for release risk assessment. This implementation follows a transparent, observable, and extensible architecture.

## Current Status: Step 1 Complete ✅

**Step 1: Say Hello to Your Agent (Basic Conversation)** has been successfully implemented with all acceptance criteria met.

## What's Implemented

### Core Components
- **Message & Conversation Models**: Immutable C# records for clean data modeling
- **Provider Abstraction**: `ILlmProvider` interface enabling multi-provider support
- **Anthropic Provider**: Full implementation of Claude 3.5 Sonnet integration
- **Agent Core**: Orchestrates conversation with LLM, maintains state
- **File System Storage**: Persistent conversation storage as JSON
- **CLI Application**: Interactive terminal interface for testing

### Features
- ✅ Back-and-forth conversation with Claude
- ✅ Conversation history maintained in memory
- ✅ Persistent storage to filesystem
- ✅ Load and continue previous conversations
- ✅ Multiple conversation management
- ✅ Provider abstraction for future providers
- ✅ Comprehensive error handling
- ✅ 7 automated xUnit tests (all passing)

## Quick Start

See [quick-start.md](quick-start.md) for detailed setup and testing instructions.

**Quick Test:**
```bash
# 1. Set your API key
$env:ANTHROPIC_API_KEY="your-key-here"

# 2. Build
dotnet build

# 3. Run tests
dotnet test

# 4. Run CLI
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
│   │   └── ProviderExceptions.cs    # Custom exceptions
│   └── Storage/
│       ├── IConversationStore.cs    # Storage interface
│       └── FileSystemConversationStore.cs
├── samples/DetectiveAgent.Cli/      # CLI application
│   ├── Program.cs                   # Main CLI logic
│   └── appsettings.json             # Configuration
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

### Step 2: Observability (Traces and Spans)
- Add OpenTelemetry instrumentation
- Capture timing, token counts, and operation flow
- Export traces to filesystem

### Step 3: Context Window Management
- Token counting and estimation
- Conversation truncation strategies
- Context budget allocation

### Step 4: Retry Mechanism
- Exponential backoff for transient failures
- Rate limit handling
- Network error recovery

### Step 5: System Prompt Engineering
- Enhanced agent personality
- Tool usage instructions
- Capability awareness

### Step 6: Tool Abstraction
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
- Persistence (save/load)
- Multiple conversation handling

## Configuration

Configure via `appsettings.json`:
```json
{
  "Agent": {
    "SystemPrompt": "You are a helpful AI assistant.",
    "Temperature": 0.7,
    "MaxTokens": 4096
  },
  "Providers": {
    "Anthropic": {
      "ApiKey": "env:ANTHROPIC_API_KEY",
      "Model": "claude-3-5-sonnet-20241022"
    }
  },
  "Storage": {
    "ConversationsPath": "./data/conversations"
  }
}
```

## Contributing

This is an educational project following the Detective Agent design specification. The implementation follows a strict step-by-step approach to demonstrate how to build production-quality agents from first principles.

## License

Educational project - see design documentation for details.

## Resources

- [Design Specification](memory/DESIGN.md) - System architecture and design decisions
- [Implementation Plan](memory/PLAN.md) - .NET-specific implementation details
- [Implementation Steps](memory/STEPS.md) - Step-by-step guide
- [Quick Start Guide](quick-start.md) - Manual testing instructions
