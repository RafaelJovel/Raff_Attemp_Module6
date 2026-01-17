# Detective Agent Implementation Plan (Python)

## Overview
Python implementation of the Detective Agent. See [DESIGN.md](DESIGN.md) for more about **what** the agent does and **why** design decisions were made.

This document covers **how** to build the agent in Python - specific packages, project structure, testing approach, and implementation details.  

## Implementation Goals
- Clear, readable Python code that shows exactly what's happening
- Multi-provider support (Anthropic, OpenRouter, Ollama, etc)
- OpenTelemetry observability
- Context window management
- Retry mechanism with exponential backoff
- Tool calling foundation
- Interaction persistence
- Basic reasoning and evaluations

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
- **Python 3.13.7** with async/await
- **uv** for dependency and venv management
- **httpx** for HTTP client (async, HTTP/2 support)
- **OpenTelemetry SDK** for traces and metrics
- **pydantic** for configuration and validation
- **pytest** + **pytest-asyncio** for testing
- **respx** for mocking httpx in tests

<instructions_for_ai_assistant>
Read @DESIGN.md and @STEPS.md. Complete the rest of this document with implementation steps that align to these design principles. The design allows for flexibility in certain areas. When you have multiple options, ask the user what their preference is - do not make assumptions or fundamental design decisions on your own.
</instructions_for_ai_assistant>