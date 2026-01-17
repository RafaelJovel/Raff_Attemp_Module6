# Detective Agent - Quick Start Guide

This guide will help you manually test the Detective Agent implementation (Step 1: Basic Conversation).

## Prerequisites

- .NET 9.0 SDK installed
- Anthropic API key (get one from https://console.anthropic.com/)

## 1. Setup

### Set your Anthropic API Key

You have two options:

**Option A: Environment Variable (Recommended)**
```bash
# Windows (PowerShell)
$env:ANTHROPIC_API_KEY="your-api-key-here"

# Windows (CMD)
set ANTHROPIC_API_KEY=your-api-key-here

# Linux/macOS
export ANTHROPIC_API_KEY="your-api-key-here"
```

**Option B: Configuration File**
Edit `samples/DetectiveAgent.Cli/appsettings.json` and add your API key:
```json
{
  "Providers": {
    "Anthropic": {
      "ApiKey": "your-api-key-here",
      ...
    }
  }
}
```

## 2. Build the Solution

```bash
# Build the entire solution
dotnet build

# Or build just the CLI project
dotnet build samples/DetectiveAgent.Cli
```

## 3. Run Automated Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal
```

Expected output: All 6 tests should pass.

## 4. Run the CLI Application

```bash
# Run the CLI
dotnet run --project samples/DetectiveAgent.Cli
```

## 5. Manual Testing Scenarios

### Test 1: Basic Conversation

1. Start the CLI application
2. Type a simple greeting: `Hello!`
3. Verify the agent responds appropriately
4. Type `exit` to quit

**Expected Result:** Agent responds with a greeting and maintains conversational context.

### Test 2: Conversation Persistence

1. Start the CLI and have a short conversation (2-3 messages)
2. Note the conversation ID shown when the CLI starts
3. Type `exit` to quit
4. Check that a JSON file was created in `data/conversations/` directory
5. Start the CLI again
6. Type `list` to see all conversations
7. Type `load <conversation-id>` using the ID from step 2
8. Type `history` to see the previous messages
9. Continue the conversation

**Expected Result:** Previous conversation is loaded and you can continue where you left off.

### Test 3: Using releases.json Data

This test simulates asking the agent to analyze release data (preparing for future tool implementation in Step 6).

1. Start the CLI
2. Ask the agent to analyze a release:
   ```
   I have a software release v2.1.0 with the following data:
   - Changes: Added payment processing, Fixed authentication bug
   - Tests: 142 passed, 2 failed, 5 skipped
   - Error rate: 0.02
   - Response time p95: 450ms
   
   Can you assess the risk level of this release?
   ```

3. Review the agent's response

**Expected Result:** The agent should analyze the data and provide a risk assessment. Without tools (Step 6), it won't have access to the actual releases.json file, but it should be able to reason about the data you provide.

### Test 4: Multiple Conversations

1. Start CLI and have a conversation about a topic (e.g., "Tell me about software testing")
2. Type `new` to start a new conversation
3. Have a different conversation (e.g., "What's the weather like?")
4. Type `list` to see both conversations
5. Load the first conversation and continue it

**Expected Result:** Both conversations are maintained separately and can be switched between.

### Test 5: Conversation History

1. Have a conversation with 5-6 message exchanges
2. Type `history` to view the conversation
3. Verify all messages are shown with timestamps

**Expected Result:** Full conversation history is displayed with proper formatting.

### Test 6: Release Risk Assessment (Manual Data Input)

Test the agent's ability to reason about release risk using data from `memory/releases.json`:

**Low Risk Release (v2.1.1):**
```
Analyze this release:
Version: v2.1.1
Changes: Documentation updates, Minor bug fix
Tests: 150 passed, 0 failed, 0 skipped
Error rate: 0.01
Response time p95: 380ms

What's the risk level?
```

**Medium Risk Release (v2.2.0):**
```
Analyze this release:
Version: v2.2.0
Changes: API endpoint updates
Tests: 145 passed, 1 failed, 4 skipped
Error rate: 0.04
Response time p95: 420ms

What's the risk level?
```

**High Risk Release (v3.0.0):**
```
Analyze this release:
Version: v3.0.0
Changes: Payment processing rewrite, Database migration
Tests: 120 passed, 8 failed, 2 skipped
Error rate: 0.03
Response time p95: 500ms

What's the risk level?
```

**Expected Result:** The agent should correctly assess risk levels based on test failures, error rates, and change impact.

## 6. Verify Conversation Storage

After running the tests above:

1. Navigate to `data/conversations/` directory
2. Open one of the JSON files
3. Verify the structure includes:
   - `id`: Conversation ID
   - `systemPrompt`: System instructions
   - `messages`: Array of user and assistant messages
   - `createdAt`: Timestamp
   - `metadata`: Additional information (provider, message count, etc.)

## 7. CLI Commands Reference

| Command | Description |
|---------|-------------|
| `exit` or `quit` | Exit the application |
| `new` | Start a new conversation |
| `list` | List all saved conversations |
| `load <id>` | Load a specific conversation by ID |
| `history` | Show current conversation history |

## Troubleshooting

### API Key Error
If you see "Anthropic API key not found":
- Ensure environment variable is set correctly
- Or add the key to `appsettings.json`

### Build Errors
If the build fails:
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Test Failures
If tests fail:
- Check that all NuGet packages are restored
- Ensure you're using .NET 9.0 or later

## Next Steps

Once Step 1 is working:
- **Step 2**: Add observability with OpenTelemetry traces
- **Step 3**: Implement context window management
- **Step 4**: Add retry mechanism for API failures
- **Step 5**: Enhance system prompt engineering
- **Step 6**: Add tool abstraction and risk assessment tools (this is where releases.json will be used programmatically)
- **Step 7**: Build evaluation system

## Project Structure

```
DetectiveAgent/
├── src/DetectiveAgent/          # Core library
│   ├── Core/                    # Agent and conversation models
│   ├── Providers/               # LLM provider abstraction
│   └── Storage/                 # Conversation persistence
├── samples/DetectiveAgent.Cli/  # CLI application
├── tests/DetectiveAgent.Tests/  # Unit tests
├── data/conversations/          # Stored conversations (created at runtime)
└── memory/                      # Design docs and test data
    └── releases.json            # Sample release data for testing
```

## Success Criteria

Step 1 is complete when:
- ✅ CLI application starts and connects to Anthropic
- ✅ User can send messages and receive responses
- ✅ Conversation history is maintained in memory
- ✅ Conversations are saved to filesystem as JSON
- ✅ Can load and continue previous conversations
- ✅ All 6 automated tests pass
- ✅ Provider abstraction is functional
- ✅ Basic error handling works
