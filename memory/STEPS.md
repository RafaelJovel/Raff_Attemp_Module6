# Detective Agent Implementation Steps (.NET)

## Recommended Order of Implementation

This section outlines an incremental, iterative approach to building the agent using .NET and C#. Each step builds on the previous one with clear acceptance criteria.

### Step 0: Create Implementation Plan
**Goal:** Translate the design document into a concrete .NET implementation plan based on the steps defined in this document.

**Tasks:**
- Review the entire design specification
- Break down each component into implementable tasks
- Identify dependencies between components
- Define .NET solution structure and project organization
- Create a detailed implementation plan document

**Acceptance Criteria:**
- Implementation plan document exists (PLAN.md)
- All components from design are represented in plan
- Dependencies are clearly identified
- .NET solution structure is defined
- Plan includes specific tasks with clear deliverables
- NuGet packages and dependencies identified

### Step 1: Say Hello to Your Agent (Basic Conversation)
**Goal:** Build a minimal working agent that can have a conversation

**Components:**
- .NET solution and project structure (DetectiveAgent.sln)
- Configuration system using appsettings.json and IConfiguration
- Message and Conversation data models (using C# records)
- Provider abstraction interface (ILlmProvider)
- Provider implementation (AnthropicProvider, OpenRouterProvider, or OllamaProvider)
- Agent core with conversation loop (no tool loop yet)
- Filesystem-based conversation persistence using System.Text.Json
- Simple CLI application for testing
- Basic xUnit tests

**Capabilities:**
- Have a back-and-forth conversation with the LLM provider
- Conversation history maintained in memory and persisted to filesystem
- Can continue conversations across CLI sessions
- Provider abstraction layer exists (even though only one provider is implemented)

**Acceptance Criteria:**
- CLI application starts and connects to LLM provider
- User can send messages and receive responses
- Conversation history is maintained in memory during session
- Each conversation is saved to filesystem as JSON using System.Text.Json
- Can load and continue previous conversations from filesystem
- Conversation includes all messages with timestamps
- Basic error handling for API failures using custom exception types
- At least 3 automated xUnit tests covering core functionality
- Provider abstraction interface (ILlmProvider) is defined and implemented
- Dependency injection configured using Microsoft.Extensions.DependencyInjection
- Configuration loaded from appsettings.json

**Key .NET Commands:**
```bash
dotnet new sln -n DetectiveAgent
dotnet new classlib -n DetectiveAgent -o src/DetectiveAgent
dotnet new console -n DetectiveAgent.Cli -o samples/DetectiveAgent.Cli
dotnet new xunit -n DetectiveAgent.Tests -o tests/DetectiveAgent.Tests
dotnet sln add src/DetectiveAgent tests/DetectiveAgent.Tests samples/DetectiveAgent.Cli
dotnet build
dotnet test
dotnet run --project samples/DetectiveAgent.Cli
```

### Step 2: Observability (Traces and Spans)
**Goal:** Add complete OpenTelemetry visibility into agent operations

**Components:**
- OpenTelemetry .NET SDK packages
- ActivitySource for creating traces and spans
- Instrumentation for agent operations using Activity API
- Instrumentation for provider calls
- Filesystem-based trace export (JSON files)
- Trace context propagation through conversation
- HTTP client instrumentation (auto-instrumented via OpenTelemetry.Instrumentation.Http)

**Capabilities:**
- Every conversation generates a trace
- Agent operations captured as spans (SendMessageAsync, provider calls)
- Traces include timing, token counts, model info
- All traces saved to filesystem as JSON
- Trace IDs link conversations to their traces
- HTTP requests automatically instrumented

**Acceptance Criteria:**
- ✅ Each conversation has a unique trace ID (Activity.TraceId)
- ✅ Traces saved to filesystem in OpenTelemetry JSON format
- ✅ Spans capture: operation name, duration, start/end times using Activity
- ✅ Provider call spans include: model, tokens (input/output), duration
- ✅ Conversation spans include: message count, total tokens
- ✅ Trace files are human-readable and well-organized
- ✅ Can correlate conversation JSON with its trace JSON via trace ID (traceId field is present in saved conversation JSON files)
- ✅ Automated tests verify trace generation
- ✅ OpenTelemetry configured in dependency injection
- ✅ ActivitySource registered as singleton (implemented as static in AgentActivitySource)

**Resolution:**
- **Root Cause**: Agent constructor was creating conversations before OpenTelemetry was initialized in Program.cs, resulting in null Activity and no TraceId capture
- **Solution Implemented**:
  1. Changed Agent to use lazy initialization - no conversation created in constructor
  2. Conversation is created on first `SendMessageAsync()` call or explicit `StartNewConversation()` call
  3. Updated Program.cs to explicitly call `StartNewConversation()` after `host.StartAsync()` to ensure OpenTelemetry is initialized
  4. Updated tests to account for lazy initialization behavior
- **Verification**: Manual testing confirmed traceId is now properly saved in conversation JSON files and can be correlated with trace files

**Key NuGet Packages:**
- OpenTelemetry
- OpenTelemetry.Exporter.Console
- OpenTelemetry.Exporter.OpenTelemetryProtocol
- OpenTelemetry.Instrumentation.Http
- OpenTelemetry.Extensions.Hosting

### Step 3: Context Window Management
**Goal:** Handle conversations that exceed model token limits

**Components:**
- Token counting/estimation (using provider-specific tokenizers or approximations)
- Truncation strategy implementation
- Token budget allocation (system prompt, history, response, buffer)
- Context management integration into agent core
- ContextWindowManager class

**Capabilities:**
- Estimate token count for conversation history
- Automatically truncate old messages when approaching limit
- Reserve tokens for system prompt and response
- Maintain conversation coherence during truncation
- Track context window utilization in traces

**Acceptance Criteria:**
- Agent calculates token count before each provider call
- Conversation truncates when within 90% of token limit
- System prompt always preserved
- Most recent N messages preserved
- Context window state visible in traces (using Activity.SetTag)
- Long conversations don't cause API errors
- Automated tests verify truncation behavior
- Token estimation configurable per provider

### Step 4: Retry Mechanism
**Goal:** Handle transient failures gracefully

**Components:**
- Retry configuration (using IOptions<RetryConfiguration>)
- Exponential backoff with jitter implementation
- Retry logic for provider calls using Microsoft.Extensions.Http.Resilience or Polly
- Error classification (retryable vs non-retryable using custom exception types)
- Retry tracking in traces

**Capabilities:**
- Automatic retry for rate limits (429) using RateLimitException
- Automatic retry for network errors
- Automatic retry for temporary server errors (500, 502, 503)
- Exponential backoff between attempts
- No retry for auth, validation, or permanent errors
- Full retry visibility in traces

**Acceptance Criteria:**
- Rate limit errors trigger retries
- Retries use exponential backoff
- Max retry attempts configurable via appsettings.json
- Jitter added to prevent thundering herd
- Auth/validation errors fail immediately (AuthenticationException)
- Retry attempts tracked in traces with timing
- Automated tests verify retry behavior using WireMock.Net
- Manual test of rate limit handling
- Resilience pipeline configured in HttpClient setup

**Key NuGet Packages:**
- Microsoft.Extensions.Http.Resilience (or Polly)

### Step 5: System Prompt Engineering
**Goal:** Give the agent personality, capability awareness, and clear instructions

**Components:**
- Enhanced system prompt with agent purpose
- Instructions for tool usage (when tools are added)
- Response format guidance
- Capability boundaries and limitations
- Configuration to customize system prompt via appsettings.json

**Capabilities:**
- Agent has clear sense of purpose
- Agent understands its capabilities
- Agent responds appropriately to out-of-scope requests
- System prompt can be customized per use case

**Acceptance Criteria:**
- Default system prompt defines agent purpose
- System prompt explains how to behave
- System prompt is easily configurable in appsettings.json
- Agent behavior reflects system prompt instructions
- Tested with various prompt configurations
- System prompt stored in configuration and injected via IOptions

### Step 6: Tool Abstraction
**Goal:** Enable agent to use external tools for release risk assessment

**Components:**
- Tool definition data model (ToolDefinition record)
- Tool call and tool result data models (ToolCall, ToolResult records)
- IToolRegistry interface and implementation
- Tool execution framework (ToolExecutor class)
- Tool loop in agent core
- Release risk assessment tools (GetReleaseSummaryTool, FileRiskReportTool)
- Tool formatting for provider-specific APIs
- Mock HTTP endpoints using WireMock.Net or static test data for tools

**Capabilities:**
- Register tools with agent using IToolRegistry
- Agent receives tool definitions in LLM calls
- LLM can request tool execution
- Agent executes tools and returns results
- Tool loop continues until LLM provides final response
- Release assessment workflow functional and tested
- Tool calls tracked in traces

**Acceptance Criteria:**
- IToolRegistry interface defined
- Tools can be registered with agent (dependency injection)
- Agent formats tools for provider API (Anthropic format, OpenRouter format, etc.)
- Tool execution loop works end-to-end
- GetReleaseSummaryTool returns mock release data
- FileRiskReportTool accepts and validates risk reports
- Tool calls and results visible in conversation history
- Tool execution captured in traces with timing using Activity
- Error handling for tool failures (ToolExecutionException)
- Automated xUnit tests for tool framework and both tools
- CLI demo of release risk assessment workflow
- Tools registered in dependency injection container

**Optional Enhancement:**
- Add web search tool (using HttpClient) as additional capability demonstration

### Step 7: Evaluation System
**Goal:** Validate agent behavior through automated evaluation

**Components:**
- Separate evaluation project (DetectiveAgent.Evaluations.csproj)
- Test case definitions with expected behaviors (C# classes)
- Tool usage evaluation (behavioral validation)
- Decision quality evaluation (output validation)
- Error handling evaluation (robustness validation)
- Regression tracking (performance over time)
- Structured report generation (machine-readable JSON output)
- Evaluation runner and reporting

**Evaluation Dimensions:**

#### 1. Tool Usage Evaluation
Validates the agent's behavioral choices:
- Does the agent call the correct tools for the task?
- Does it call them in a reasonable order?
- Does it provide valid parameters?
- Does it handle tool errors appropriately?

**Example Test Cases:**
```csharp
public class HighRiskScenario : IEvaluationScenario
{
    public string ScenarioId => "high_risk_release";
    
    public ReleaseData ReleaseData => new()
    {
        Version = "v2.1.0",
        Changes = ["Payment processing"],
        Tests = new TestResults 
        { 
            Passed = 140, 
            Failed = 5 
        },
        DeploymentMetrics = new() 
        { 
            ErrorRate = 0.08 
        }
    };
    
    public string[] ExpectedTools => 
        ["get_release_summary", "file_risk_report"];
        
    public string ExpectedToolOrder => "get before post";
}
```

#### 2. Decision Quality Evaluation
Validates the agent's risk assessment accuracy:
- Does the severity classification match expected severity?
- Are key risks identified in the report?
- Is the reasoning sound given the data?

**Example Evaluation:**
```csharp
public class DecisionQualityEvaluator : IEvaluator
{
    public EvaluationResult Evaluate(
        AgentOutput agentOutput, 
        ExpectedOutput expected)
    {
        var severityCorrect = 
            agentOutput.Severity == expected.Severity;
            
        var risksIdentified = CalculateOverlap(
            agentOutput.Findings,
            expected.KeyRisks
        );
        
        return new EvaluationResult
        {
            SeverityCorrect = severityCorrect,
            RiskRecall = risksIdentified,
            Passed = severityCorrect && risksIdentified >= 0.7
        };
    }
}
```

#### 3. Error Handling Evaluation
Validates the agent's robustness and error handling:
- Does the agent handle missing or malformed data gracefully?
- Does it report errors clearly to the user?
- Does it avoid hallucinating data when tools fail?
- Does it make appropriate decisions when data is incomplete?

#### 4. Regression Tracking
Monitors evaluation performance over time:
- Establishes a baseline of expected performance
- Detects when changes degrade agent behavior
- Tracks improvements over time
- Provides comparison reports showing deltas

**Key Features:**
- Save baseline results for future comparison (JSON files)
- Compare current results to baseline
- Identify regressions (performance drops >5%)
- Identify improvements (performance gains >5%)
- Track overall score and pass rate trends

#### 5. Structured Report Generation
Produces machine-readable evaluation output:
- Summary metrics (pass rate, average score)
- Per-scenario results with status and scores
- Regression comparison data
- Designed for CI/CD integration
- Enables automated quality gates

**Test Scenarios:**
- High risk: Failed tests in critical areas, elevated error rates
- Medium risk: Minor test failures, slight metric degradation
- Low risk: All tests passing, healthy metrics
- Error handling: Missing releases, malformed data, tool failures
- Edge cases: Missing data fields, API errors, unexpected responses

**Acceptance Criteria:**
- Evaluation framework can run test scenarios automatically
- Tool usage evaluated for correctness and ordering
- Decision quality measured against expected outcomes
- Error handling scenarios validate robustness
- Test suite includes 5+ scenarios covering risk spectrum and error cases
- Regression tracking compares to baseline
- Structured JSON reports generated for automation using System.Text.Json
- Evaluation results include pass/fail and diagnostic details
- Automated xUnit tests verify evaluation framework itself
- Documentation explains how to add new evaluation cases
- CLI command supports baseline establishment and comparison
- Separate evaluation project with its own dependencies

**Key .NET Commands:**
```bash
dotnet new xunit -n DetectiveAgent.Evaluations -o tests/DetectiveAgent.Evaluations
dotnet sln add tests/DetectiveAgent.Evaluations
dotnet add tests/DetectiveAgent.Evaluations reference src/DetectiveAgent
dotnet test tests/DetectiveAgent.Evaluations
```

**Future Evaluation Options:**
- Conversation quality: Can the agent explain its reasoning when asked?
- Robustness testing: How does it handle errors, timeouts, edge cases?
- Performance benchmarks: Latency, token efficiency, tool call efficiency
- LLM-as-judge: Use another LLM to evaluate response quality

### Step 8: Additional Capabilities (Future)

**Goal:** Expand agent capabilities with more tools and features

**Potential Additions:**
- Additional tools (file operations, calculator, web search via HttpClient, etc.)
- Multi-provider support (expand beyond initial provider)
- Advanced context management (summarization, importance-based)
- Async tool execution with Task.WhenAll
- Tool composition and chaining
- User confirmation for sensitive tools
- Feedback loops and self-correction
- Conversation branching and exploration
- Progressive investigation (drill-down tools)
- Multi-agent coordination using separate Agent instances
- ASP.NET Core API for agent interactions
- Blazor UI for conversational interface
- Azure deployment options (Azure Container Apps, Azure Functions)
- Azure OpenAI Service integration
- Semantic Kernel integration for advanced scenarios

**Note:** These are not prescribed steps but rather areas for future exploration based on specific use cases and requirements.

## .NET-Specific Implementation Notes

### Modern C# Features to Leverage
- **Records** for immutable data models (Message, Conversation, ToolDefinition)
- **Nullable reference types** enabled for compile-time null safety
- **Pattern matching** for error classification and message handling
- **Async streams** (IAsyncEnumerable) for streaming responses (future)
- **Init-only properties** for configuration objects
- **Required members** for ensuring critical properties are set
- **File-scoped namespaces** to reduce indentation
- **Global usings** to reduce boilerplate

### Dependency Injection Best Practices
- Register services in Program.cs or Startup.cs
- Use IOptions<T> pattern for configuration
- Scope services appropriately (Singleton, Scoped, Transient)
- Use IHttpClientFactory for all HTTP calls
- Register ActivitySource as singleton

### Testing Best Practices
- Use xUnit as the test framework
- Moq or NSubstitute for mocking
- WireMock.Net for HTTP integration tests
- Arrange-Act-Assert pattern
- Test async methods properly with async/await
- Use IClassFixture for expensive setup
- Use [Theory] and [InlineData] for parameterized tests

### Performance Considerations
- Use ValueTask<T> for hot paths that often complete synchronously
- Leverage Span<T> and Memory<T> for high-performance scenarios
- Use System.Text.Json for efficient serialization
- Consider object pooling for frequently allocated objects
- Profile with BenchmarkDotNet if performance is critical

### Deployment Options
- Self-contained deployment for CLI tool
- Docker containerization
- Azure Container Apps for cloud deployment
- Azure Functions for serverless scenarios
- Windows Service or systemd service for background processing
