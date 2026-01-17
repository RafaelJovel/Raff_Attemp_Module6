# Detective Agent Implementation Plan (Python)

## Overview
Python implementation of the Detective Agent. See [DESIGN.md](DESIGN.md)for more about **what** the agent does and **why** design decisions were made.

This document covers **how** to build the agent in Python - specific packages, project structure, testing approach, and implementation details.  

## Implementation Constitution
- Clear, readable Python code that shows exactly what's happening
- For interfaces, use Protocol, and DO NOT use an ABC
- Place unit tests in the folder as the code under test
- Unit tests have a `_test.py` suffix and DO NOT have a `test_` prefix
- The `/tests` folder should only contain integration tests and common test assets
- When you're running tests and Python scripts, remember that the `python` binary is in the virtual environment
- Use `uv venv` to create the venv and `uv add` when adding dependencies
- Never use `pip` or `uv pip` and never create `requirements.txt`

## Implementation Steps
The recommended order of implementation is defined in [STEPS.md](STEPS.md). The phases of implementation defined later in this document align with these progression of steps.

## Technology Stack
- **Python 3.14+** with async/await
- **uv** for dependency and venv management
- **httpx** for HTTP client (async, HTTP/2 support)
- **OpenTelemetry SDK** for traces and metrics
- **pydantic** for model validation
- **pydantic-settings** for configuration (.env)
- **pytest** + **pytest-asyncio** for testing
- **respx** for mocking httpx in tests

## Project Structure
```
detective-agent/
├── src/
│   └── detective_agent/
│       ├── __init__.py
│       ├── models.py              # Data models (Message, Conversation, Tool, etc.)
│       ├── models_test.py
│       ├── config.py              # Configuration and settings
│       ├── agent.py               # Agent core orchestration
│       ├── agent_test.py
│       ├── providers/
│       │   ├── __init__.py
│       │   ├── base.py            # Provider abstraction interface
│       │   ├── ollama.py          # Ollama provider implementation
│       │   └── ollama_test.py
│       ├── tools/
│       │   ├── __init__.py
│       │   ├── base.py            # Tool abstraction framework
│       │   ├── release_tools.py   # Release assessment tools
│       │   └── release_tools_test.py
│       ├── context/
│       │   ├── __init__.py
│       │   ├── truncation.py      # Context window management
│       │   └── truncation_test.py
│       ├── retry/
│       │   ├── __init__.py
│       │   ├── backoff.py         # Retry mechanism with exponential backoff
│       │   └── backoff_test.py
│       ├── observability/
│       │   ├── __init__.py
│       │   └── tracing.py         # OpenTelemetry instrumentation
│       └── storage/
│           ├── __init__.py
│           └── filesystem.py      # Conversation persistence
├── tests/
│   ├── __init__.py
│   └── fixtures.py
├── evals/
│   ├── __init__.py
│   ├── scenarios.py               # Test scenarios and expected behaviors
│   ├── evaluators.py              # Evaluation logic
│   ├── regression.py              # Regression tracking and baseline comparison
│   └── run_evals.py               # Eval runner
├── data/
│   ├── conversations/             # Saved conversation JSON files
│   ├── traces/                    # Saved trace JSON files
│   └── evals/                     # Evaluation results and baseline
├── cli.py                         # Interactive CLI interface
├── pyproject.toml                 # uv project configuration
└── README.md
```

### Provider Selection
- **First Implementation**: Ollama (local model execution)
- **Ollama Advantages**:
  - No API keys required
  - Free local execution
  - Good for development and testing
  - Supports tool calling with compatible models
- **Recommended Model**: llama3.1 or qwen2.5 (both support tool calling)

### Context Window Strategy
- **Strategy**: Simple truncation with message limit
- **Configuration**: Keep last 6 messages (3 user + 3 assistant pairs)
- **Implementation**:
  - Always preserve system prompt
  - Keep most recent N messages (configurable, default 6)
  - Discard older messages when limit exceeded
  - Track truncation events in traces
- **Token Buffer**: Reserve 10% safety margin for estimation errors

### Conversation Persistence
- **Format**: JSON files in `data/conversations/` directory
- **Filename Pattern**: `conversation_{id}_{timestamp}.json`
- **Contents**: Full conversation object including metadata and trace ID

### Trace Export
- **Format**: OpenTelemetry JSON in `data/traces/` directory
- **Filename Pattern**: `trace_{trace_id}.json`
- **Export**: File-based exporter (can be extended to OTLP later)

## Phase 1: Basic Conversation (Step 1)

### 1.1 Data Models (`models.py`)
Implement core data structures using Pydantic:

```python
@dataclass
class Message:
    role: Literal["user", "assistant", "system"]
    content: str
    timestamp: datetime
    metadata: dict = field(default_factory=dict)

@dataclass
class Conversation:
    id: str
    system_prompt: str
    messages: list[Message]
    created_at: datetime
    metadata: dict = field(default_factory=dict)

@dataclass
class ToolDefinition:
    name: str
    description: str
    parameters: dict  # JSON schema

@dataclass
class ToolCall:
    id: str
    name: str
    arguments: dict
    timestamp: datetime

@dataclass
class ToolResult:
    tool_call_id: str
    content: Any
    success: bool
    timestamp: datetime
    metadata: dict = field(default_factory=dict)
```

**Tests**: Validate model creation, serialization, and validation

### 1.2 Configuration (`config.py`)
Environment-based configuration using Pydantic Settings:

```python
class OllamaConfig(BaseModel):
    base_url: str = "http://localhost:11434"
    model: str = "llama3.1"
    temperature: float = 0.7
    timeout: int = 120

class AgentConfig(BaseModel):
    system_prompt: str = "You are a helpful assistant..."
    max_messages: int = 6  # For truncation
    ollama: OllamaConfig = Field(default_factory=OllamaConfig)
    data_dir: Path = Path("./data")
```

**Tests**: Configuration loading, defaults, validation

### 1.3 Provider Abstraction (`providers/base.py`)
Define the provider interface:

```python
class Provider(Protocol):
    async def complete(
        self,
        messages: list[Message],
        tools: list[ToolDefinition] | None = None,
        **kwargs
    ) -> Message:
        """Send messages to LLM and get response"""
        ...

    def estimate_tokens(self, messages: list[Message]) -> int:
        """Estimate token count for messages"""
        ...

    def get_capabilities(self) -> dict:
        """Return provider capabilities"""
        ...
```

### 1.4 Ollama Provider (`providers/ollama.py`)
Implement Ollama-specific provider:

```python
class OllamaProvider(Provider):
    def __init__(self, config: OllamaConfig):
        self.config = config
        self.client = httpx.AsyncClient(base_url=config.base_url)

    async def complete(self, messages, tools=None, **kwargs):
        # Format messages for Ollama API
        # POST /api/chat with proper format
        # Handle tool calls if present in response
        # Return Message object
        pass

    def estimate_tokens(self, messages):
        # Simple estimation: ~4 chars per token
        # Can be enhanced with tiktoken for better accuracy
        pass

    def get_capabilities(self):
        return {
            "tools": True,  # llama3.1 supports tools
            "streaming": True,
            "vision": False
        }
```

**Ollama API Format**:
- Endpoint: `POST /api/chat`
- Message format: `{"role": "user", "content": "..."}`
- Tool support: Via `tools` parameter in request
- Response includes `message` and optional `tool_calls`

**Tests**: Mock Ollama API, test message formatting, tool call handling

### 1.5 Agent Core (`agent.py`)
Implement basic conversation orchestration (no tool loop yet):

```python
class DetectiveAgent:
    def __init__(self, config: AgentConfig, provider: Provider):
        self.config = config
        self.provider = provider
        self.conversation = self._new_conversation()
        self.tools: dict[str, ToolDefinition] = {}

    async def send_message(self, content: str) -> Message:
        # 1. Create user message
        user_msg = Message(role="user", content=content, ...)
        self.conversation.messages.append(user_msg)

        # 2. Get response from provider (no context mgmt or tools yet)
        assistant_msg = await self.provider.complete(
            messages=self.conversation.messages
        )

        # 3. Add assistant response
        self.conversation.messages.append(assistant_msg)

        return assistant_msg

    def get_history(self, limit: int | None = None) -> list[Message]:
        msgs = self.conversation.messages
        return msgs[-limit:] if limit else msgs

    def _new_conversation(self) -> Conversation:
        return Conversation(
            id=str(uuid.uuid4()),
            system_prompt=self.config.system_prompt,
            messages=[],
            created_at=datetime.now(),
            metadata={}
        )
```

**Tests**: Message sending, history retrieval, conversation state

### 1.6 Filesystem Storage (`storage/filesystem.py`)
Save and load conversations:

```python
class FilesystemStorage:
    def __init__(self, data_dir: Path):
        self.conversations_dir = data_dir / "conversations"
        self.conversations_dir.mkdir(parents=True, exist_ok=True)

    def save_conversation(self, conversation: Conversation) -> Path:
        filename = f"conversation_{conversation.id}_{int(conversation.created_at.timestamp())}.json"
        path = self.conversations_dir / filename

        with open(path, 'w') as f:
            json.dump(conversation.to_dict(), f, indent=2, default=str)

        return path

    def load_conversation(self, conversation_id: str) -> Conversation:
        # Find latest file for this conversation ID
        # Load and deserialize
        pass

    def list_conversations(self) -> list[dict]:
        # Return list of conversation metadata
        pass
```

**Tests**: Save, load, list operations

### 1.7 CLI Interface (`cli.py`)
Simple interactive command-line interface:

```python
async def main():
    # Load config
    config = AgentConfig()

    # Initialize provider and agent
    provider = OllamaProvider(config.ollama)
    agent = DetectiveAgent(config, provider)
    storage = FilesystemStorage(config.data_dir)

    print("Detective Agent CLI")
    print("Commands: /save, /load <id>, /history, /new, /quit")

    while True:
        user_input = input("\nYou: ").strip()

        if user_input.startswith("/"):
            # Handle commands
            continue

        response = await agent.send_message(user_input)
        print(f"\nAgent: {response.content}")
```

**Tests**: Manual testing of CLI interaction

## Phase 2: Observability (Step 2)

### 2.1 Tracing Setup (`observability/tracing.py`)
Initialize OpenTelemetry and create instrumentation utilities:

```python
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import SimpleSpanProcessor
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter

def init_tracing(service_name: str, export_dir: Path):
    # Set up tracer provider
    provider = TracerProvider(resource=Resource.create({
        "service.name": service_name
    }))

    # File-based JSON exporter
    processor = SimpleSpanProcessor(
        FileSpanExporter(export_dir / "traces")
    )
    provider.add_span_processor(processor)

    trace.set_tracer_provider(provider)

    return trace.get_tracer(service_name)

class FileSpanExporter:
    """Custom exporter that saves spans as JSON files"""
    def __init__(self, output_dir: Path):
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)

    def export(self, spans):
        for span in spans:
            trace_id = span.context.trace_id
            filename = f"trace_{trace_id:032x}.json"
            # Save span data as JSON
            pass
```

### 2.2 Agent Instrumentation
Add tracing to agent operations:
- A trace represents an entire conversation (1 trace per conversation)
- Each trace should be placed in its own file
- Each message, response, or other operation should create a new span
- All spans belonging to a trace should be saved in the trace file

```python
class DetectiveAgent:
    def __init__(self, config, provider):
        # ... existing init ...
        self.tracer = trace.get_tracer(__name__)

    async def send_message(self, content: str) -> Message:
        with self.tracer.start_as_current_span(
            "agent.send_message",
            attributes={
                "conversation.id": self.conversation.id,
                "message.role": "user",
                "message.length": len(content)
            }
        ) as span:
            # ... existing logic ...

            # Add result attributes
            span.set_attribute("message.count", len(self.conversation.messages))
            span.set_attribute("response.length", len(assistant_msg.content))

            return assistant_msg
```

### 2.3 Provider Instrumentation
Add tracing to provider calls:

```python
class OllamaProvider:
    async def complete(self, messages, tools=None, **kwargs):
        with self.tracer.start_as_current_span(
            "provider.complete",
            attributes={
                "provider.name": "ollama",
                "provider.model": self.config.model,
                "message.count": len(messages)
            }
        ) as span:
            start_time = time.time()

            # ... API call ...

            duration = time.time() - start_time
            span.set_attribute("provider.duration_ms", duration * 1000)
            span.set_attribute("provider.input_tokens", input_tokens)
            span.set_attribute("provider.output_tokens", output_tokens)

            return response
```

**Tests**: Verify span creation, attributes, trace file generation

## Phase 3: Context Window Management (Step 3)

### 3.1 Token Estimation
Add token counting to provider:

```python
class OllamaProvider:
    def estimate_tokens(self, messages: list[Message]) -> int:
        # Simple estimation: ~4 characters per token
        total_chars = sum(len(msg.content) for msg in messages)
        return total_chars // 4

    def get_context_limit(self) -> int:
        # Model-specific limits
        limits = {
            "llama3.1": 128_000,
            "qwen2.5": 32_000,
        }
        return limits.get(self.config.model, 8_000)
```

### 3.2 Truncation Strategy (`context/truncation.py`)
Implement message truncation:

```python
class TruncationStrategy:
    def __init__(self, max_messages: int = 6):
        self.max_messages = max_messages

    def manage_context(
        self,
        messages: list[Message],
        system_prompt: str,
        provider: Provider
    ) -> tuple[list[Message], dict]:
        """
        Returns: (managed_messages, metadata)
        """
        # Always keep system message
        # Keep last N messages
        if len(messages) > self.max_messages:
            truncated = messages[-self.max_messages:]
            metadata = {
                "truncated": True,
                "messages_removed": len(messages) - self.max_messages,
                "messages_kept": len(truncated)
            }
        else:
            truncated = messages
            metadata = {"truncated": False}

        # Verify token budget
        total_tokens = provider.estimate_tokens(truncated)
        metadata["estimated_tokens"] = total_tokens

        return truncated, metadata
```

### 3.3 Integration with Agent
Apply context management before provider calls:

```python
class DetectiveAgent:
    def __init__(self, config, provider):
        # ... existing ...
        self.context_strategy = TruncationStrategy(
            max_messages=config.max_messages
        )

    async def send_message(self, content: str) -> Message:
        with self.tracer.start_as_current_span("agent.send_message") as span:
            # ... add user message ...

            # Manage context
            managed_msgs, ctx_metadata = self.context_strategy.manage_context(
                self.conversation.messages,
                self.conversation.system_prompt,
                self.provider
            )

            # Add context info to span
            span.set_attributes(ctx_metadata)

            # Call provider with managed messages
            assistant_msg = await self.provider.complete(messages=managed_msgs)

            # ... rest of logic ...
```

**Tests**: Verify truncation triggers, message preservation, token estimation

## Phase 4: Retry Mechanism (Step 4)

### 4.1 Retry Configuration
Add retry settings to config:

```python
class RetryConfig(BaseModel):
    max_attempts: int = 3
    initial_delay: float = 1.0  # seconds
    max_delay: float = 60.0
    backoff_factor: float = 2.0
    jitter: bool = True
```

### 4.2 Retry Logic (`retry/backoff.py`)
Implement exponential backoff with jitter:

```python
class RetryManager:
    def __init__(self, config: RetryConfig):
        self.config = config

    async def execute_with_retry(
        self,
        operation: Callable,
        *args,
        **kwargs
    ):
        last_error = None

        for attempt in range(self.config.max_attempts):
            try:
                with trace.get_tracer(__name__).start_as_current_span(
                    "retry.attempt",
                    attributes={"attempt": attempt + 1}
                ) as span:
                    result = await operation(*args, **kwargs)
                    span.set_attribute("retry.success", True)
                    return result

            except Exception as e:
                last_error = e

                if not self._is_retryable(e):
                    raise

                if attempt < self.config.max_attempts - 1:
                    delay = self._calculate_delay(attempt)
                    await asyncio.sleep(delay)

        raise last_error

    def _is_retryable(self, error: Exception) -> bool:
        # Check if error is retryable
        # Rate limits (429), network errors, 500/502/503
        if isinstance(error, httpx.HTTPStatusError):
            return error.response.status_code in [429, 500, 502, 503]
        if isinstance(error, (httpx.NetworkError, httpx.TimeoutException)):
            return True
        return False

    def _calculate_delay(self, attempt: int) -> float:
        delay = min(
            self.config.initial_delay * (self.config.backoff_factor ** attempt),
            self.config.max_delay
        )

        if self.config.jitter:
            delay *= (0.5 + random.random() * 0.5)  # 50-100% of delay

        return delay
```

### 4.3 Provider Integration
Wrap provider calls with retry logic:

```python
class OllamaProvider:
    def __init__(self, config: OllamaConfig):
        # ... existing ...
        self.retry_manager = RetryManager(RetryConfig())

    async def complete(self, messages, tools=None, **kwargs):
        return await self.retry_manager.execute_with_retry(
            self._complete_internal,
            messages,
            tools,
            **kwargs
        )

    async def _complete_internal(self, messages, tools, **kwargs):
        # Actual API call logic here
        pass
```

**Tests**: Mock retryable errors, verify backoff timing, ensure non-retryable fails immediately

## Phase 5: System Prompt Engineering (Step 5)

### 5.1 Detective Agent System Prompt
Create a purpose-driven system prompt:

```python
DEFAULT_SYSTEM_PROMPT = """You are a Detective Agent specializing in software release risk assessment.

Your role is to analyze release information and identify potential risks that could impact deployment success or system stability.

When analyzing releases, consider:
- Test results: Failed tests indicate potential bugs or regressions
- Code changes: High-risk areas include authentication, payments, data handling
- Deployment metrics: Error rates, response times, resource usage
- Change scope: Large changes carry more risk than small fixes

You have access to tools that let you:
1. Retrieve release summaries (version, changes, test results, metrics)
2. File risk reports with severity assessments

Risk severity guidelines:
- HIGH: Test failures in critical areas, elevated error rates (>5%), major architectural changes
- MEDIUM: Minor test failures, slight metric degradation (2-5% error rate), moderate changes
- LOW: All tests passing, healthy metrics (<2% error rate), minor changes

Always:
- Call get_release_summary to retrieve release data before analysis
- Provide clear, actionable findings in risk reports
- Base severity on evidence from the data
- Be thorough but concise in your assessments

Be helpful, precise, and focused on identifying genuine risks."""
```

### 5.2 Configuration
Make system prompt easily customizable:

```python
class AgentConfig(BaseModel):
    system_prompt: str = DEFAULT_SYSTEM_PROMPT
    # ... other config ...

    @classmethod
    def from_file(cls, path: Path):
        # Allow loading custom system prompts from file
        with open(path) as f:
            prompt = f.read()
        return cls(system_prompt=prompt)
```

**Tests**: Test agent behavior with different prompts, verify tool usage improves

## Phase 6: Tool Abstraction and Release Assessment (Step 6)

### 6.1 Tool Framework (`tools/base.py`)
Implement tool registration and execution:

```python
class ToolRegistry:
    def __init__(self):
        self.tools: dict[str, ToolDefinition] = {}
        self.handlers: dict[str, Callable] = {}

    def register(
        self,
        name: str,
        description: str,
        parameters: dict,
        handler: Callable
    ):
        tool_def = ToolDefinition(
            name=name,
            description=description,
            parameters=parameters
        )
        self.tools[name] = tool_def
        self.handlers[name] = handler

    async def execute(
        self,
        tool_call: ToolCall
    ) -> ToolResult:
        if tool_call.name not in self.handlers:
            return ToolResult(
                tool_call_id=tool_call.id,
                content=f"Tool '{tool_call.name}' not found",
                success=False,
                timestamp=datetime.now()
            )

        try:
            handler = self.handlers[tool_call.name]
            result = await handler(**tool_call.arguments)

            return ToolResult(
                tool_call_id=tool_call.id,
                content=result,
                success=True,
                timestamp=datetime.now()
            )
        except Exception as e:
            return ToolResult(
                tool_call_id=tool_call.id,
                content=str(e),
                success=False,
                timestamp=datetime.now(),
                metadata={"error_type": type(e).__name__}
            )

    def format_for_provider(self, provider_name: str) -> list[dict]:
        # Convert tool definitions to provider-specific format
        # Ollama uses OpenAI-compatible format
        return [
            {
                "type": "function",
                "function": {
                    "name": tool.name,
                    "description": tool.description,
                    "parameters": tool.parameters
                }
            }
            for tool in self.tools.values()
        ]
```

### 6.2 Release Assessment Tools (`tools/release_tools.py`)
Implement the two core tools:

```python
# Mock release data for testing
MOCK_RELEASES = {
    "v2.1.0": {
        "version": "v2.1.0",
        "changes": ["Added payment processing", "Fixed authentication bug"],
        "tests": {"passed": 142, "failed": 2, "skipped": 5},
        "deployment_metrics": {
            "error_rate": 0.02,
            "response_time_p95": 450
        }
    }
}

async def get_release_summary(release_id: str) -> dict:
    """
    Retrieve release summary information.

    Args:
        release_id: The release identifier (e.g., "v2.1.0")

    Returns:
        Release metadata including version, changes, tests, metrics
    """
    if release_id in MOCK_RELEASES:
        return MOCK_RELEASES[release_id]
    else:
        raise ValueError(f"Release {release_id} not found")

RISK_REPORTS = []  # Store filed reports

async def file_risk_report(
    release_id: str,
    severity: str,
    findings: list[str]
) -> dict:
    """
    File a risk assessment report for a release.

    Args:
        release_id: The release identifier
        severity: Risk level ("high", "medium", "low")
        findings: List of identified risks or concerns

    Returns:
        Confirmation with report ID
    """
    if severity.lower() not in ["high", "medium", "low"]:
        raise ValueError(f"Invalid severity: {severity}")

    report = {
        "report_id": str(uuid.uuid4()),
        "release_id": release_id,
        "severity": severity.lower(),
        "findings": findings,
        "filed_at": datetime.now().isoformat()
    }

    RISK_REPORTS.append(report)

    return {
        "status": "success",
        "report_id": report["report_id"],
        "message": f"Risk report filed for {release_id}"
    }

def register_release_tools(registry: ToolRegistry):
    """Register both release assessment tools"""

    registry.register(
        name="get_release_summary",
        description="Retrieve release summary including version, changes, test results, and deployment metrics",
        parameters={
            "type": "object",
            "properties": {
                "release_id": {
                    "type": "string",
                    "description": "The release identifier (e.g., 'v2.1.0')"
                }
            },
            "required": ["release_id"]
        },
        handler=get_release_summary
    )

    registry.register(
        name="file_risk_report",
        description="File a risk assessment report for a release with severity and findings",
        parameters={
            "type": "object",
            "properties": {
                "release_id": {
                    "type": "string",
                    "description": "The release identifier"
                },
                "severity": {
                    "type": "string",
                    "enum": ["high", "medium", "low"],
                    "description": "Risk severity level"
                },
                "findings": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "List of identified risks or concerns"
                }
            },
            "required": ["release_id", "severity", "findings"]
        },
        handler=file_risk_report
    )
```

### 6.3 Tool Calling Loop in Agent
Implement the tool execution loop:

```python
class DetectiveAgent:
    def __init__(self, config, provider):
        # ... existing ...
        self.tool_registry = ToolRegistry()

    def register_tool(self, name, description, parameters, handler):
        self.tool_registry.register(name, description, parameters, handler)

    async def send_message(self, content: str) -> Message:
        with self.tracer.start_as_current_span("agent.send_message") as span:
            # Add user message
            user_msg = Message(role="user", content=content, timestamp=datetime.now())
            self.conversation.messages.append(user_msg)

            # Tool calling loop
            max_iterations = 10  # Prevent infinite loops
            iteration = 0

            while iteration < max_iterations:
                iteration += 1

                # Manage context
                managed_msgs, ctx_metadata = self.context_strategy.manage_context(
                    self.conversation.messages,
                    self.conversation.system_prompt,
                    self.provider
                )

                # Get tools in provider format
                tools = self.tool_registry.format_for_provider("ollama")

                # Call provider
                assistant_msg = await self.provider.complete(
                    messages=managed_msgs,
                    tools=tools if tools else None
                )

                # Add assistant message
                self.conversation.messages.append(assistant_msg)

                # Check for tool calls
                tool_calls = assistant_msg.metadata.get("tool_calls", [])

                if not tool_calls:
                    # No tools requested - we're done
                    span.set_attribute("tool_iterations", iteration)
                    return assistant_msg

                # Execute tools and add results
                for tool_call_data in tool_calls:
                    tool_call = ToolCall(
                        id=tool_call_data["id"],
                        name=tool_call_data["name"],
                        arguments=tool_call_data["arguments"],
                        timestamp=datetime.now()
                    )

                    with self.tracer.start_as_current_span(
                        "tool.execute",
                        attributes={"tool.name": tool_call.name}
                    ):
                        result = await self.tool_registry.execute(tool_call)

                    # Add tool result as a message
                    tool_result_msg = Message(
                        role="tool",
                        content=json.dumps(result.content),
                        timestamp=result.timestamp,
                        metadata={
                            "tool_call_id": result.tool_call_id,
                            "tool_name": tool_call.name,
                            "success": result.success
                        }
                    )
                    self.conversation.messages.append(tool_result_msg)

            raise RuntimeError(f"Tool loop exceeded max iterations ({max_iterations})")
```

### 6.4 Ollama Tool Calling Support
Update provider to handle tool calls:

```python
class OllamaProvider:
    async def _complete_internal(self, messages, tools, **kwargs):
        # Format messages for Ollama
        formatted_msgs = [
            {"role": msg.role, "content": msg.content}
            for msg in messages
        ]

        # Build request
        request = {
            "model": self.config.model,
            "messages": formatted_msgs,
            "stream": False,
            "options": {
                "temperature": self.config.temperature
            }
        }

        # Add tools if provided
        if tools:
            request["tools"] = tools

        # Make API call
        response = await self.client.post(
            "/api/chat",
            json=request,
            timeout=self.config.timeout
        )
        response.raise_for_status()

        data = response.json()
        msg_data = data["message"]

        # Extract content and tool calls
        content = msg_data.get("content", "")
        tool_calls = msg_data.get("tool_calls", [])

        return Message(
            role="assistant",
            content=content,
            timestamp=datetime.now(),
            metadata={
                "model": self.config.model,
                "tool_calls": tool_calls,
                "eval_count": data.get("eval_count", 0),  # output tokens
                "prompt_eval_count": data.get("prompt_eval_count", 0)  # input tokens
            }
        )
```

**Tests**:
- Tool registration and retrieval
- Tool execution with valid/invalid arguments
- Tool calling loop with mock responses
- End-to-end release assessment workflow

### 6.5 CLI Enhancement
Add tool testing commands:

```python
async def main():
    # ... setup ...

    # Register release tools
    from detective_agent.tools.release_tools import register_release_tools
    register_release_tools(agent.tool_registry)

    print("Detective Agent CLI")
    print("Commands: /save, /load <id>, /history, /new, /quit")
    print("Try: 'Assess release v2.1.0'")

    # ... rest of CLI ...
```

## Phase 7: Evaluation System (Step 7)

### 7.1 Test Scenarios (`evals/scenarios.py`)
Define evaluation test cases:

```python
EVAL_SCENARIOS = [
    {
        "id": "high_risk_failed_tests",
        "description": "High risk: Multiple test failures",
        "release_data": {
            "version": "v3.0.0",
            "changes": ["Payment processing rewrite", "Database migration"],
            "tests": {"passed": 120, "failed": 8, "skipped": 2},
            "deployment_metrics": {"error_rate": 0.03, "response_time_p95": 500}
        },
        "expected": {
            "tools_called": ["get_release_summary", "file_risk_report"],
            "tool_order": "get_before_post",
            "severity": "high",
            "key_risks": ["test failures", "payment", "database"]
        }
    },
    {
        "id": "medium_risk_elevated_errors",
        "description": "Medium risk: Elevated error rate",
        "release_data": {
            "version": "v2.2.0",
            "changes": ["API endpoint updates"],
            "tests": {"passed": 145, "failed": 1, "skipped": 4},
            "deployment_metrics": {"error_rate": 0.04, "response_time_p95": 420}
        },
        "expected": {
            "tools_called": ["get_release_summary", "file_risk_report"],
            "severity": "medium",
            "key_risks": ["error rate", "failed test"]
        }
    },
    {
        "id": "low_risk_clean",
        "description": "Low risk: Clean release",
        "release_data": {
            "version": "v2.1.1",
            "changes": ["Documentation updates", "Minor bug fix"],
            "tests": {"passed": 150, "failed": 0, "skipped": 0},
            "deployment_metrics": {"error_rate": 0.01, "response_time_p95": 380}
        },
        "expected": {
            "tools_called": ["get_release_summary", "file_risk_report"],
            "severity": "low",
            "key_risks": []
        }
    },
    {
        "id": "tool_error_missing_release",
        "description": "Error handling: Release not found",
        "release_data": None,  # Won't be injected - release doesn't exist
        "input": "Assess the risks for release v99.99.99",
        "expected": {
            "handles_error": True,
            "error_keywords": ["not found", "unable to retrieve", "does not exist"],
            "tools_called": ["get_release_summary"],
            "files_report": False
        }
    },
    {
        "id": "malformed_data_missing_tests",
        "description": "Error handling: Missing test data",
        "release_data": {
            "version": "v4.0.0",
            "changes": ["New feature"],
            # Missing 'tests' field
            "deployment_metrics": {
                "error_rate": 0.01,
                "response_time_p95": 400
            }
        },
        "expected": {
            "handles_error": True,
            "tools_called": ["get_release_summary"],
            "files_report": "optional"  # Can pass either way
        }
    }
]
```

### 7.2 Evaluators (`evals/evaluators.py`)
Implement evaluation logic:

```python
class ToolUsageEvaluator:
    def evaluate(self, conversation: Conversation, expected: dict) -> dict:
        """Evaluate if agent called correct tools in correct order"""

        # Extract tool calls from conversation
        tool_calls = []
        for msg in conversation.messages:
            if msg.role == "assistant" and "tool_calls" in msg.metadata:
                for tc in msg.metadata["tool_calls"]:
                    tool_calls.append(tc["name"])

        # Check if expected tools were called
        expected_tools = set(expected["tools_called"])
        actual_tools = set(tool_calls)

        tools_correct = expected_tools == actual_tools

        # Check order if specified
        order_correct = True
        if "tool_order" in expected:
            if expected["tool_order"] == "get_before_post":
                get_idx = tool_calls.index("get_release_summary") if "get_release_summary" in tool_calls else -1
                post_idx = tool_calls.index("file_risk_report") if "file_risk_report" in tool_calls else -1
                order_correct = get_idx < post_idx if get_idx >= 0 and post_idx >= 0 else False

        return {
            "tools_correct": tools_correct,
            "order_correct": order_correct,
            "expected_tools": list(expected_tools),
            "actual_tools": list(actual_tools),
            "pass": tools_correct and order_correct
        }

class DecisionQualityEvaluator:
    def evaluate(self, conversation: Conversation, expected: dict) -> dict:
        """Evaluate if risk assessment matches expected severity and findings"""

        # Find the filed risk report in conversation
        report = None
        for msg in conversation.messages:
            if msg.role == "tool" and "file_risk_report" in msg.metadata.get("tool_name", ""):
                report = json.loads(msg.content)
                break

        if not report:
            return {
                "severity_correct": False,
                "findings_recall": 0.0,
                "pass": False,
                "error": "No risk report filed"
            }

        # Extract severity from report arguments (in tool call)
        # This requires tracking tool call arguments
        # Simplified: check if severity mentioned in findings

        # Check severity
        # This would need access to the actual filed report
        # For now, simplified version

        return {
            "severity_correct": True,  # Placeholder
            "findings_recall": 0.8,  # Placeholder
            "pass": True
        }

class ErrorHandlingEvaluator:
    """Evaluates how the agent handles errors and edge cases"""

    def evaluate(self, conversation: Conversation, expected: dict) -> dict:
        """Evaluate error handling behavior.

        Args:
            conversation: The conversation to evaluate
            expected: Expected error handling containing:
                - handles_error: Whether agent should handle gracefully
                - error_keywords: Keywords that should appear in response
                - files_report: Whether report should still be filed
        """
        # Get the agent's final response
        assistant_messages = [
            msg for msg in conversation.messages
            if msg.role == "assistant"
        ]

        if not assistant_messages:
            return {
                "handles_gracefully": False,
                "error": "No assistant response found",
                "pass": False
            }

        final_response = assistant_messages[-1].content.lower()

        # Check if error keywords are mentioned
        error_keywords = expected.get("error_keywords", [])
        keywords_found = [
            kw for kw in error_keywords
            if kw.lower() in final_response
        ]

        mentions_error = len(keywords_found) > 0

        # Check if risk report was filed
        reports = get_risk_reports()
        filed_report = len(reports) > 0

        files_report_expected = expected.get("files_report", False)

        # Evaluate based on expectations
        if files_report_expected == "optional":
            # Either filing or not filing is acceptable
            report_correct = True
        else:
            report_correct = filed_report == files_report_expected

        # Check for hallucinations (making up data)
        hallucination_indicators = [
            "passed: " in final_response and "failed: " in final_response,
            "error rate:" in final_response and "%" in final_response
        ]
        might_hallucinate = any(hallucination_indicators) and not expected.get("release_data")

        handles_gracefully = (
            mentions_error and
            report_correct and
            not might_hallucinate
        )

        return {
            "handles_gracefully": handles_gracefully,
            "mentions_error": mentions_error,
            "keywords_found": keywords_found,
            "filed_report": filed_report,
            "report_correct": report_correct,
            "might_hallucinate": might_hallucinate,
            "pass": handles_gracefully
        }

class CompositeEvaluator:
    """Evaluates using the appropriate evaluators based on scenario type"""

    def __init__(self):
        self.tool_evaluator = ToolUsageEvaluator()
        self.decision_evaluator = DecisionQualityEvaluator()
        self.error_evaluator = ErrorHandlingEvaluator()

    def evaluate(self, conversation: Conversation, expected: dict) -> dict:
        # Check if this is an error handling scenario
        if expected.get("handles_error"):
            error_results = self.error_evaluator.evaluate(conversation, expected)

            # For error scenarios, we only care about error handling
            return {
                "error_handling": error_results,
                "overall_pass": error_results["pass"],
                "score": 1.0 if error_results["pass"] else 0.0
            }

        # Normal scenarios - evaluate tool usage and decision quality
        tool_results = self.tool_evaluator.evaluate(conversation, expected)
        decision_results = self.decision_evaluator.evaluate(conversation, expected)

        # Calculate composite score
        tool_weight = 0.4
        decision_weight = 0.6

        score = (
            (1.0 if tool_results["pass"] else 0.0) * tool_weight +
            (1.0 if decision_results["pass"] else 0.0) * decision_weight
        )

        overall_pass = tool_results["pass"] and decision_results["pass"]

        return {
            "tool_usage": tool_results,
            "decision_quality": decision_results,
            "overall_pass": overall_pass,
            "score": score
        }
```

### 7.3 Eval Runner (`evals/run_evals.py`)
Execute evaluation scenarios:

```python
async def run_evaluation(scenario: dict, agent: DetectiveAgent) -> dict:
    """Run a single evaluation scenario"""

    # Reset agent state
    agent.conversation = agent._new_conversation()

    # Inject scenario release data into mock data (if provided)
    if scenario.get("release_data"):
        from detective_agent.tools.release_tools import MOCK_RELEASES
        MOCK_RELEASES[scenario["release_data"]["version"]] = scenario["release_data"]

    # Send assessment request
    prompt = scenario.get("input") or f"Assess the risks for release {scenario['release_data']['version']}"

    try:
        response = await agent.send_message(prompt)

        # Use composite evaluator
        evaluator = CompositeEvaluator()
        evaluation = evaluator.evaluate(agent.conversation, scenario["expected"])

        return {
            "scenario_id": scenario["id"],
            "success": True,
            "evaluation": evaluation,
            "overall_pass": evaluation["overall_pass"]
        }

    except Exception as e:
        return {
            "scenario_id": scenario["id"],
            "success": False,
            "error": str(e)
        }

def generate_structured_report(report: dict) -> dict:
    """Generate a structured report for programmatic consumption.

    This format is designed to be easily parsed by:
    - CI/CD systems (fail builds on regressions)
    - Monitoring dashboards
    - Other agents or analysis tools
    """
    structured = {
        "summary": {
            "timestamp": report["timestamp"],
            "total_scenarios": report["total_scenarios"],
            "passed": report["passed"],
            "failed": report["failed"],
            "success_rate": report["success_rate"],
            "average_score": report["average_score"]
        },
        "scenarios": []
    }

    for result in report["results"]:
        if not result["success"]:
            structured["scenarios"].append({
                "id": result["scenario_id"],
                "status": "error",
                "error": result.get("error")
            })
            continue

        eval_result = result["evaluation"]
        structured["scenarios"].append({
            "id": result["scenario_id"],
            "status": "pass" if eval_result["overall_pass"] else "fail",
            "score": eval_result["score"]
        })

    # Add regression info if available
    if "regression_comparison" in report:
        structured["regression"] = {
            "baseline_timestamp": report["regression_comparison"]["baseline_timestamp"],
            "score_delta": report["regression_comparison"]["score_delta"],
            "regressions": report["regression_comparison"]["regression_count"],
            "improvements": report["regression_comparison"]["improvement_count"]
        }

    return structured

async def run_all_evals(save_results: bool = True, update_baseline: bool = False):
    """Run all evaluation scenarios and generate report"""

    # Initialize agent
    config = AgentConfig()
    provider = OllamaProvider(config.ollama)
    agent = DetectiveAgent(config, provider)

    # Register tools
    from detective_agent.tools.release_tools import register_release_tools
    register_release_tools(agent.tool_registry)

    # Run scenarios
    results = []
    for scenario in EVAL_SCENARIOS:
        print(f"Running scenario: {scenario['id']}")
        result = await run_evaluation(scenario, agent)
        results.append(result)
        print(f"  Result: {'PASS' if result.get('overall_pass') else 'FAIL'}")

    # Calculate metrics
    passed = sum(1 for r in results if r.get("overall_pass"))
    failed = len(results) - passed
    success_rate = passed / len(results) if results else 0.0

    # Calculate average score
    scores = [r["evaluation"]["score"] for r in results if r.get("success")]
    average_score = sum(scores) / len(scores) if scores else 0.0

    # Build report
    report = {
        "timestamp": datetime.now().isoformat(),
        "total_scenarios": len(results),
        "passed": passed,
        "failed": failed,
        "success_rate": success_rate,
        "average_score": average_score,
        "results": results
    }

    # Print human-readable report
    print(f"\n{'=' * 70}")
    print("Evaluation Report")
    print(f"{'=' * 70}")
    print(f"Timestamp: {report['timestamp']}")
    print(f"Scenarios: {report['total_scenarios']}")
    print(f"Passed: {passed}/{report['total_scenarios']}")
    print(f"Success Rate: {success_rate:.1%}")
    print(f"Average Score: {average_score:.3f}")

    # Check for baseline and compare
    from evals.regression import compare_to_baseline, save_baseline, print_regression_report
    baseline_path = Path("data/evals/baseline.json")
    comparison = compare_to_baseline(report, baseline_path)

    if comparison:
        print_regression_report(comparison)
        report["regression_comparison"] = comparison
    else:
        print("\n📝 No baseline found. Run with --baseline to set baseline.")

    # Save results
    if save_results:
        output_dir = Path("data/evals")
        output_dir.mkdir(parents=True, exist_ok=True)

        timestamp_str = datetime.now().strftime("%Y%m%d_%H%M%S")

        # Save detailed results
        output_file = output_dir / f"eval_results_{timestamp_str}.json"
        with open(output_file, "w") as f:
            json.dump(report, f, indent=2, default=str)

        # Save structured report
        structured_file = output_dir / f"eval_report_{timestamp_str}.json"
        structured = generate_structured_report(report)
        with open(structured_file, "w") as f:
            json.dump(structured, f, indent=2)

        print(f"\n📄 Detailed results: {output_file}")
        print(f"📊 Structured report: {structured_file}")

        # Update baseline if requested
        if update_baseline:
            save_baseline(report, baseline_path)

    return report

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Run evaluations")
    parser.add_argument("--baseline", action="store_true",
                       help="Update the baseline with these results")
    parser.add_argument("--no-save", action="store_true",
                       help="Don't save results to disk")

    args = parser.parse_args()

    asyncio.run(run_all_evals(
        save_results=not args.no_save,
        update_baseline=args.baseline
    ))
```

**Tests**:
- Individual evaluator unit tests
- End-to-end eval runner
- Report generation
- Regression comparison logic

### 7.4 Regression Tracking (`evals/regression.py`)
Track evaluation performance over time to detect regressions:

```python
import json
from pathlib import Path
from typing import Optional

def save_baseline(results: dict, baseline_path: Path) -> None:
    """Save evaluation results as the baseline for future comparisons."""
    with open(baseline_path, 'w') as f:
        json.dump(results, f, indent=2, default=str)
    print(f"✅ Baseline saved to {baseline_path}")

def compare_to_baseline(
    current_results: dict,
    baseline_path: Path
) -> Optional[dict]:
    """Compare current results to baseline.

    Returns:
        Comparison dict with regressions/improvements, or None if no baseline
    """
    if not baseline_path.exists():
        return None

    with open(baseline_path) as f:
        baseline = json.load(f)

    # Compare overall metrics
    current_score = current_results['average_score']
    baseline_score = baseline['average_score']
    score_delta = current_score - baseline_score

    current_pass_rate = current_results['success_rate']
    baseline_pass_rate = baseline['success_rate']
    pass_rate_delta = current_pass_rate - baseline_pass_rate

    # Find regressions and improvements per scenario
    regressions = []
    improvements = []

    baseline_by_id = {
        r['scenario_id']: r
        for r in baseline['results']
        if r.get('success')
    }

    for current in current_results['results']:
        if not current.get('success'):
            continue

        scenario_id = current['scenario_id']
        if scenario_id not in baseline_by_id:
            continue

        baseline_scenario = baseline_by_id[scenario_id]

        current_score = current['evaluation']['score']
        baseline_score = baseline_scenario['evaluation']['score']
        delta = current_score - baseline_score

        if delta < -0.05:  # Score dropped by >5%
            regressions.append({
                'scenario_id': scenario_id,
                'baseline_score': baseline_score,
                'current_score': current_score,
                'delta': delta
            })
        elif delta > 0.05:  # Score improved by >5%
            improvements.append({
                'scenario_id': scenario_id,
                'baseline_score': baseline_score,
                'current_score': current_score,
                'delta': delta
            })

    return {
        'baseline_timestamp': baseline['timestamp'],
        'score_delta': score_delta,
        'pass_rate_delta': pass_rate_delta,
        'regressions': regressions,
        'improvements': improvements,
        'regression_count': len(regressions),
        'improvement_count': len(improvements)
    }

def print_regression_report(comparison: dict) -> None:
    """Print a formatted regression comparison report."""
    print("\n" + "=" * 70)
    print("Regression Comparison")
    print("=" * 70)
    print(f"Baseline: {comparison['baseline_timestamp']}")

    # Overall changes
    score_delta = comparison['score_delta']
    score_emoji = "📈" if score_delta > 0 else "📉" if score_delta < 0 else "➡️"
    print(f"\n{score_emoji} Average Score: {score_delta:+.3f}")

    pass_rate_delta = comparison['pass_rate_delta']
    pass_emoji = "📈" if pass_rate_delta > 0 else "📉" if pass_rate_delta < 0 else "➡️"
    print(f"{pass_emoji} Pass Rate: {pass_rate_delta:+.1%}")

    # Regressions
    if comparison['regressions']:
        print(f"\n❌ Regressions ({len(comparison['regressions'])})")
        for reg in comparison['regressions']:
            print(f"  {reg['scenario_id']}: {reg['baseline_score']:.2f} → {reg['current_score']:.2f} ({reg['delta']:.2f})")

    # Improvements
    if comparison['improvements']:
        print(f"\n✅ Improvements ({len(comparison['improvements'])})")
        for imp in comparison['improvements']:
            print(f"  {imp['scenario_id']}: {imp['baseline_score']:.2f} → {imp['current_score']:.2f} ({imp['delta']:+.2f})")

    if not comparison['regressions'] and not comparison['improvements']:
        print("\n➡️  No significant changes from baseline")
```

**CLI Usage:**
```bash
# First run - establish baseline
python -m evals.run_evals --baseline

# Future runs - compare to baseline
python -m evals.run_evals

# Update baseline after improvements
python -m evals.run_evals --baseline
```

**Learning Value:**
- Teaches importance of tracking changes over time
- Shows how to detect regressions early
- Demonstrates simple but effective comparison logic
- Enables continuous monitoring of agent quality

**Tests**:
- Baseline save and load
- Comparison logic with various deltas
- Regression and improvement detection

## Testing Strategy

### Unit Tests
- Data models: serialization, validation
- Context management: truncation logic, token estimation
- Retry logic: backoff calculation, error classification
- Tool framework: registration, execution, error handling

### Integration Tests
- Provider integration: Ollama API calls (with mocking)
- Agent operations: full conversation flows
- Storage: save/load conversations
- Observability: trace generation and export

### End-to-End Tests
- CLI workflows: start, converse, save, load
- Release assessment: full tool calling workflow
- Error scenarios: API failures, tool errors, retries

### Manual Testing
- Run CLI and have conversations
- Test release assessment with various scenarios
- Verify trace files are generated correctly
- Check context truncation with long conversations

## Dependencies (`pyproject.toml`)

```toml
[project]
name = "detective-agent"
version = "0.1.0"
description = "A foundational LLM agent for software release risk assessment"
requires-python = ">=3.14"
dependencies = [
    "httpx>=0.27.0",
    "pydantic>=2.0.0",
    "pydantic-settings>=2.0.0",
    "opentelemetry-api>=1.20.0",
    "opentelemetry-sdk>=1.20.0",
]

[project.optional-dependencies]
dev = [
    "pytest>=8.0.0",
    "pytest-asyncio>=0.23.0",
    "respx>=0.21.0",
    "ruff>=0.1.0",
]

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[tool.pytest.ini_options]
asyncio_mode = "auto"
testpaths = ["tests"]

[tool.ruff]
line-length = 100
target-version = "py314"
```

## Implementation Checklist

Based on STEPS.md, here's the recommended order:

- [ ] **Step 1: Basic Conversation**
  - [ ] Data models (Message, Conversation)
  - [ ] Configuration system
  - [ ] Provider abstraction interface
  - [ ] Ollama provider implementation
  - [ ] Agent core (no tools yet)
  - [ ] Filesystem storage
  - [ ] Basic CLI
  - [ ] Unit tests

- [ ] **Step 2: Observability**
  - [ ] OpenTelemetry setup
  - [ ] Tracer initialization
  - [ ] Agent instrumentation
  - [ ] Provider instrumentation
  - [ ] File-based span export
  - [ ] Tests for trace generation

- [ ] **Step 3: Context Window Management**
  - [ ] Token estimation in provider
  - [ ] Truncation strategy implementation
  - [ ] Integration with agent
  - [ ] Context tracking in traces
  - [ ] Tests for truncation behavior

- [ ] **Step 4: Retry Mechanism**
  - [ ] Retry configuration
  - [ ] Exponential backoff logic
  - [ ] Error classification
  - [ ] Integration with provider
  - [ ] Retry tracking in traces
  - [ ] Tests for retry behavior

- [ ] **Step 5: System Prompt Engineering**
  - [ ] Detective agent system prompt
  - [ ] Configuration for custom prompts
  - [ ] Testing with different prompts

- [ ] **Step 6: Tool Abstraction**
  - [ ] Tool framework (registry, execution)
  - [ ] Release assessment tools
  - [ ] Tool calling loop in agent
  - [ ] Ollama tool calling support
  - [ ] CLI tool demo
  - [ ] End-to-end tool tests

- [ ] **Step 7: Evaluation System**
  - [ ] Test scenarios definition (including error handling scenarios)
  - [ ] Tool usage evaluator
  - [ ] Decision quality evaluator
  - [ ] Error handling evaluator
  - [ ] Composite evaluator
  - [ ] Eval runner with structured reports
  - [ ] Regression tracking and baseline comparison
  - [ ] Report generation (human-readable and machine-readable)
  - [ ] Automated eval tests

## Future Enhancements (Step 8)

After completing the core implementation, consider:

1. **Additional Providers**: OpenRouter, Anthropic, OpenAI
2. **Advanced Context Management**: Sliding window with summarization
3. **More Tools**: Web search, file operations, calculator
4. **Async Tool Execution**: Parallel tool calls
5. **Streaming Responses**: Real-time output in CLI
6. **Cost Tracking**: Estimate and track API costs
7. **Progressive Investigation**: Multi-turn tool workflows
8. **LLM-as-Judge Evals**: Use another LLM to evaluate responses
9. **Web UI**: FastAPI + React interface
10. **OTLP Export**: Send traces to Jaeger/Tempo/etc