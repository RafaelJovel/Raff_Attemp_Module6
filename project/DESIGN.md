# Detective Agent Design Specification

## Overview
A foundational LLM agent design built from first principles. This document describes the **what** and **why** of the system architecture - independent of programming language or specific technology choices.

## Use Case: Release Confidence System

**System Context:** A Release Confidence System helps build confidence in software releases by investigating changes, assessing risks, and producing actionable reports.

**Agent Role:** The Detective Agent is the first line of defense in this system. It discovers what changed in a release and identifies potential risks that warrant investigation.

**Scenario:**
When a new release is ready for deployment, the Detective Agent:
1. Retrieves a high-level release summary (code changes, test results, deployment metrics)
2. Analyzes the summary to identify potential risks
3. Files a risk report with severity assessment and key findings

The agent has access to two tools:
- **GET /release-summary**: Retrieves release metadata including changes, test results, and metrics
- **POST /risk-report**: Files a risk assessment with severity level and identified concerns

**Example Release Summary:**
```json
{
  "version": "v2.1.0",
  "changes": ["Added payment processing", "Fixed authentication bug"],
  "tests": {"passed": 142, "failed": 2, "skipped": 5},
  "deployment_metrics": {
    "error_rate": 0.02,
    "response_time_p95": 450
  }
}
```

**Expected Behavior:**
- High risk: Test failures in critical areas, elevated error rates, risky changes
- Medium risk: Minor test failures, slight metric degradation
- Low risk: All tests passing, clean metrics, low-impact changes

**Future Scenario Options:**
- **Progressive Investigation**: Add GET /test-details tool so agent can drill deeper when initial summary shows concerning signals
- **Multi-Source Correlation**: Add GET /code-changes and GET /observability-traces tools to correlate changes across systems
- **Time-Series Analysis**: Enable agent to compare current release metrics with historical baselines

## Design Philosophy

1. **Transparency Over Magic**: Every component and abstraction must be justified and minimal
2. **Observability First**: Complete visibility into agent behavior and performance
3. **Provider Agnostic**: Support multiple LLM providers through a common interface
4. **Tool-Enabled**: Enable agent capabilities through external tools
5. **Resilient**: Graceful handling of failures and retries
6. **Extensible**: Clean interfaces for future capabilities (tools, multi-agent, etc.)

## Core Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                         Agent Core                          │
│  - Conversation Management                                  │
│  - Message Orchestration                                    │
│  - Context Window Management                                │
│  - Tool Execution Loop                                      │
└─────────────────┬───────────────────────────────────────────┘
                  │
        ┌─────────┴──────────┬───────────────────┐
        │                    │                   │
┌───────▼────────┐   ┌───────▼────────┐   ┌──────▼───────┐
│    Provider    │   │      Tool      │   │  Retry       │
│   Abstraction  │   │   Abstraction  │   │  Manager     │
└───────┬────────┘   └───────┬────────┘   └──────┬───────┘
        │                    │                   │
   ┌────┴────┐          ┌────┴────┐         ┌────┴────┐
   │Provider │          │  Tool   │         │ Backoff │
   │  Impls  │          │  Impls  │         │ Strategy│
   └─────────┘          └─────────┘         └─────────┘
        │
   ┌────┴─────────────┐
   │                  │
┌──▼──────┐   ┌───────▼───────┐
│Anthropic│   │   OpenRouter  │
└─────────┘   └───────────────┘

        ┌──────────────────┐
        │  Observability   │
        │    (Traces,      │
        │   Metrics, Logs) │
        └──────────────────┘
          (Cross-cutting)
```

## Data Models

### Message
Represents a single message in a conversation.

**Properties:**
- `role`: Identifies the message sender (user, assistant, system)
- `content`: The message text content
- `timestamp`: When the message was created
- `metadata`: Extensible storage for provider-specific data, token counts, etc.

**Design Rationale:**
- Simple, immutable structure
- Provider-agnostic (maps to all LLM APIs)
- Metadata allows for future extensibility without breaking changes

### Conversation
Represents an ongoing dialogue between user and agent.

**Properties:**
- `id`: Unique identifier for this conversation
- `system_prompt`: Initial instructions/context for the agent
- `messages`: Ordered list of messages in the conversation
- `created_at`: Conversation start time
- `metadata`: Provider info, model name, conversation-level stats

**Design Rationale:**
- Encapsulates all state needed to resume a conversation
- System prompt separate from messages (some providers require this)
- Metadata tracks conversation-level information (total tokens, cost, etc.)

### Tool Definition
Represents a callable tool that the agent can use.

**Properties:**
- `name`: Unique identifier for the tool
- `description`: What the tool does (used by LLM to decide when to call it)
- `parameters`: JSON schema defining expected inputs
- `handler`: Function that executes the tool logic

**Design Rationale:**
- Simple, declarative tool registration
- Provider-agnostic definition (maps to different tool calling formats)
- Schema-driven validation ensures type safety

### Tool Call
Represents a request from the LLM to execute a tool.

**Properties:**
- `id`: Unique identifier for this tool call
- `name`: Which tool to execute
- `arguments`: JSON object with tool parameters
- `timestamp`: When the call was requested

### Tool Result
Represents the outcome of executing a tool.

**Properties:**
- `tool_call_id`: Links back to the originating tool call
- `content`: The result (success data or error message)
- `success`: Boolean indicating success/failure
- `timestamp`: When execution completed
- `metadata`: Execution time, error details, etc.

## Component Specifications

### 1. Provider Abstraction

**Purpose:** Enable swapping LLM providers without changing agent logic.

**Interface:**
```
complete(messages, temperature, max_tokens, ...) -> Message
  - Send messages to LLM provider
  - Return assistant's response
  - Handle provider-specific formatting

estimate_tokens(messages) -> int
  - Estimate token count for messages
  - Used for context window management

get_capabilities() -> ProviderCapabilities
  - Report what this provider supports
  - (tools, vision, streaming, etc.)
```

**Providers to Support:**
- **OpenRouter** (Various models via OpenRouter API)
- **Anthropic** (Claude models via Messages API)
- **Ollama** (Local models via Ollama API)

**Design Considerations:**
- Keep interface minimal - don't abstract away important provider differences
- Each provider should handle its own API format translation
- Provider should report capabilities (some may not support tool calling, etc.)
- Errors should be normalized (rate limit, auth, network, validation)

**Error Handling:**
- `AuthenticationError`: Invalid API key or credentials
- `RateLimitError`: Provider rate limiting (should trigger retry)
- `ValidationError`: Invalid request format
- `NetworkError`: Connection issues (should trigger retry)
- `ProviderError`: Other provider-specific errors

### 2. Tool Abstraction

**Purpose:** Enable agent to interact with external systems and extend its capabilities.

**Interface:**
```
register_tool(definition) -> void
  - Register a new tool with the agent
  - Validates tool definition schema
  - Makes tool available for LLM to call

execute_tool(tool_call) -> ToolResult
  - Execute a specific tool with given arguments
  - Validate arguments against schema
  - Handle execution errors gracefully
  - Return structured result

get_tools() -> list[ToolDefinition]
  - Get all registered tools
  - Used to send tool definitions to LLM provider

format_tools_for_provider(provider) -> ProviderToolFormat
  - Convert tool definitions to provider-specific format
  - Anthropic uses specific JSON schema
  - Ollama and OpenRouter may use different format
```

**Built-in Tools:**
- **Get Release Summary**: Retrieve high-level release information
  - Parameters: release_id (string)
  - Returns: Release metadata including version, changes, tests, metrics
  - Implementation: HTTP GET to mock endpoint or static test data

- **File Risk Report**: Submit risk assessment for a release
  - Parameters: release_id (string), severity (string), findings (list[string])
  - Returns: Confirmation with report ID
  - Implementation: HTTP POST to mock endpoint or file write

**Tool Execution Flow:**
1. LLM indicates it wants to call a tool (returns tool_use block)
2. Agent validates tool call against registered tools
3. Agent executes tool handler with provided arguments
4. Agent formats result into tool_result message
5. Agent sends tool_result back to LLM
6. LLM processes result and continues conversation

**Error Handling:**
- `ToolNotFoundError`: Requested tool not registered
- `InvalidArgumentsError`: Arguments don't match schema
- `ToolExecutionError`: Tool handler raised an exception
- `ToolTimeoutError`: Tool execution exceeded time limit

**Future Considerations:**
- Async tool execution
- Tool execution sandboxing/safety
- Tool composition (tools calling other tools)
- User confirmation for sensitive tools

### 3. Context Window Management

**Purpose:** Ensure conversation history fits within model's token limits.

**Strategy Options:**

#### A. Truncation (Simplest)
- Keep system prompt + N most recent messages
- Discard older messages when limit approached
- **Pros**: Simple, predictable
- **Cons**: Loses important context from earlier in conversation

#### B. Sliding Window with Summarization
- Keep system prompt + summary of old messages + recent messages
- When window fills:
  1. Take oldest N messages
  2. Ask LLM to generate summary
  3. Replace messages with summary
  4. Repeat as needed
- **Pros**: Retains key information, bounded size
- **Cons**: Summarization costs tokens/time

#### C. Importance-Based Pruning
- Score messages by importance (user messages weighted higher)
- Keep system prompt + high-importance messages + recent messages
- Prune low-importance messages when needed
- **Pros**: Keeps critical context
- **Cons**: Complex scoring, may lose temporal coherence

**Implementation Approach:**
```
manage_context(conversation, provider_limits) -> ManagedContext
  - Input: full conversation, provider's token/message limits
  - Output: pruned/summarized conversation that fits limits
  - Strategy: configurable (truncation, sliding window, importance)
```

**Token Budget Allocation:**
- Reserve tokens for system prompt (fixed)
- Reserve tokens for response (max_tokens parameter)
- Remaining budget for conversation history
- Safety margin (10% buffer for estimation errors)

### 4. Retry Mechanism

**Purpose:** Handle transient failures gracefully without losing conversation state.

**Retry Scenarios:**
- Rate limiting (429 errors)
- Network timeouts
- Temporary server errors (500, 502, 503)

**Non-Retry Scenarios:**
- Authentication errors (401, 403)
- Validation errors (400)
- Not found (404)
- Permanent errors

**Retry Strategy:**
```
execute_with_retry(operation, retry_config) -> Result
  - Attempt operation
  - On retryable failure:
    - Wait with exponential backoff
    - Retry up to max_attempts
    - Track retry attempts in observability
  - On non-retryable failure:
    - Fail immediately with clear error
```

**Retry Configuration:**
- `max_attempts`: Maximum retry attempts (e.g., 3)
- `initial_delay`: First retry delay (e.g., 1s)
- `max_delay`: Cap on backoff delay (e.g., 60s)
- `backoff_factor`: Multiplier for exponential backoff (e.g., 2)
- `jitter`: Add randomness to prevent thundering herd

**Example Retry Timeline:**
```
Attempt 1: Fails (429 Rate Limit)
Wait: 1s + jitter
Attempt 2: Fails (429 Rate Limit)
Wait: 2s + jitter
Attempt 3: Success
```

### 5. Agent Core

**Purpose:** Orchestrate all components to enable conversation with LLM.

**Responsibilities:**
- Maintain conversation state
- Coordinate provider calls
- Manage context window before each call
- Execute tool calling loop
- Handle retries for failed requests
- Emit observability events

**Key Operations:**
```
send_message(content) -> AssistantResponse
  1. Create user message
  2. Add to conversation
  3. Manage context window (prune/summarize if needed)
  4. Call provider with retry logic
  5. Check if response contains tool calls
  6. If tool calls:
     a. Execute each tool
     b. Add tool results to conversation
     c. Call provider again (loop back to step 4)
  7. If no tool calls:
     a. Add assistant response to conversation
     b. Return response to caller

register_tool(tool_def) -> void
  - Register a tool for the agent to use
  - Tool becomes available in all subsequent LLM calls

get_history(limit) -> list[Message]
  - Retrieve conversation history
  - Optional limit for recent N messages

get_context() -> ConversationContext
  - Get metadata about current conversation
  - Message count, token usage, provider info, trace IDs, tool calls

new_conversation() -> void
  - Start fresh conversation
  - Clear current messages
  - Retain configuration (system prompt, provider, tools, etc.)
```

**State Management:**
- Agent holds current conversation in memory
- Conversation is mutable (messages added over time)
- Tool definitions registered at agent initialization

### 6. Observability System

**Purpose:** Complete visibility into agent behavior without cluttering business logic.

**Design Principle:** Observability should be _as invisible as reasonable_ in the code but comprehensive in output. Use OpenTelemetry formats. This is most easily done using the language-specific SDKs.

**Observability Layers:**

#### Traces
- **Conversation Trace**: Entire user interaction
- **Agent Operation Spans**: send_message, context management, tool execution
- **Provider Call Spans**: LLM API request/response
- **HTTP Request Spans**: Individual HTTP calls (auto-instrumented)
- **Tool Execution Spans**: Individual tool calls with timing
- **Retry Spans**: Track retry attempts and backoff

**Trace Attributes:**
- Standard OpenTelemetry fields
- Conversation ID
- Message count
- Token counts (input, output, total)
- Model name, provider name
- Temperature, max_tokens
- Context window state (tokens used, tokens available)
- Tool call stats (tool name, execution time, success/failure)
- Retry info (attempt number, backoff time)

#### Metrics
- **LLM Call Duration**: Time for provider.complete()
- **Token Usage**: Input/output tokens per call
- **Error Rates**: By provider, by error type
- **Retry Rates**: How often retries are needed
- **Tool Execution Duration**: Time per tool call
- **Tool Success Rates**: Success/failure by tool
- **Context Window Utilization**: Percentage of limit used
- **Costs**: Estimated cost per call (based on provider pricing)

#### Logs
- Errors with full context
- Retry attempts with reasons
- Context window pruning events
- Tool execution events (calls, results, errors)

**Instrumentation Approach:**
- Decorators for automatic function tracing
- Context managers for custom spans
- Middleware for HTTP auto-instrumentation
- Structured logging with correlation IDs

**Trace Context Propagation:**
- Each conversation has a trace ID
- All operations link to conversation trace
- Trace IDs included in saved interactions
- Enables correlation between interactions and traces
- Each trace should be placed in its own file
- Each message, response, or other operation should create a new span
- All spans belonging to a trace should be saved in the trace file

### 7. Evaluation System

**Purpose:** Validate agent behavior through automated evaluation and regression tracking.

**Design Principle:** Unlike traditional software testing that verifies deterministic behavior, agent evaluations must account for the probabilistic nature of LLMs while maintaining quality standards.

**Interface:**
```
run_evaluation(test_suite) -> EvaluationResults
  - Execute test scenarios against agent
  - Measure behavior across multiple dimensions
  - Generate structured results

save_baseline(results, version_id) -> void
  - Store results as baseline for future comparison
  - Enable regression detection

compare_to_baseline(current, baseline) -> Comparison
  - Calculate deltas for all metrics
  - Identify regressions and improvements
  - Flag significant changes
```

**Evaluation Dimensions:**

#### Tool Usage (Behavioral Validation)
- Does the agent call the correct tools?
- Are they called in a reasonable order?
- Are parameters valid and appropriate?
- Are tool errors handled gracefully?

#### Decision Quality (Output Validation)
- Does severity classification match expected levels?
- Are key risks identified in findings?
- Is reasoning sound given the data?
- Does the agent avoid hallucinating information?

#### Error Handling (Robustness Validation)
- Does the agent handle missing data gracefully?
- Are errors reported clearly?
- Does it make appropriate decisions with incomplete information?
- Does it degrade gracefully rather than fail?

**Test Suite Composition:**
- **Risk Assessment Scenarios**: High, medium, low risk releases with expected behaviors
- **Edge Cases**: Missing data, boundary values, ambiguous signals
- **Error Scenarios**: Tool failures, malformed responses, timeouts

**Evaluation Approaches:**
- **Ground Truth Comparison**: Compare output to known correct answers (high confidence)
- **Rubric-Based Scoring**: Define criteria and score responses (structured assessment)
- **LLM-as-Judge**: Use separate LLM to evaluate quality (subjective dimensions)

**Regression Tracking:**
- Establish baseline performance metrics
- Track pass rates, scores, and behavior over time
- Detect degradations (>5% drop triggers warning)
- Monitor improvements and changes

**Report Format:**
```json
{
  "summary": {
    "pass_rate": 0.85,
    "total_scenarios": 20,
    "avg_scores": {"tool_usage": 0.92, "decision_quality": 0.81}
  },
  "scenarios": [{"id": "high_risk", "status": "passed", "scores": {...}}],
  "regression_analysis": {"regressions": [...], "improvements": [...]}
}
```

**Design Rationale:**
- Machine-readable output enables CI/CD integration
- Multi-dimensional scoring captures different quality aspects
- Regression tracking prevents silent quality erosion
- Structured reports speed up debugging and improvement