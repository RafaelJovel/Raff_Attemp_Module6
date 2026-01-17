# Ollama Provider Setup Guide

## Overview

The Detective Agent now supports Ollama as an LLM provider, allowing you to run local language models in your Docker container named `quizzical_shannon`.

## What Was Added

### 1. OllamaProvider Implementation
- **File**: `src/DetectiveAgent/Providers/OllamaProvider.cs`
- Implements the `ILlmProvider` interface
- Uses Ollama's OpenAI-compatible API format
- Supports conversation history with system/user/assistant roles
- Handles model loading and response parsing

### 2. Configuration Support
- **File**: `samples/DetectiveAgent.Cli/appsettings.json`
- Added Ollama provider configuration section
- Set as the default provider

### 3. Dynamic Provider Selection
- **File**: `samples/DetectiveAgent.Cli/Program.cs`
- Updated to support provider switching via configuration
- Automatically configures the correct provider based on `Agent:DefaultProvider` setting

## Current Configuration

Your agent is currently configured to use:
- **Container**: `quizzical_shannon` (ID: a2398b780c2c)
- **Container IP**: `172.17.0.2`
- **Port**: `11434`
- **Model**: `qwen2.5:0.5b` (a fast, lightweight 0.5B parameter model)
- **Base URL**: `http://172.17.0.2:11434`

## Testing the Connection

Run the CLI application to test the Ollama connection:

```bash
dotnet run --project samples/DetectiveAgent.Cli
```

You should see:
```
Using Ollama provider with model: qwen2.5:0.5b at http://172.17.0.2:11434
═══════════════════════════════════════════════════════════
   Detective Agent CLI
═══════════════════════════════════════════════════════════
```

Then try a simple message:
```
You: Hello, who are you?
```

The agent should respond using the local Ollama model.

## Switching Between Providers

### To use Anthropic instead:
1. Open `samples/DetectiveAgent.Cli/appsettings.json`
2. Change `"DefaultProvider": "Ollama"` to `"DefaultProvider": "Anthropic"`
3. Ensure you have a valid Anthropic API key configured

### To use a different Ollama model:
1. Pull the model into your container:
   ```bash
   docker exec quizzical_shannon ollama pull <model-name>
   ```
   Popular options:
   - `llama3.2:3b` - Llama 3.2 3B (faster, good quality)
   - `qwen2.5:7b` - Qwen 2.5 7B (better quality, slower)
   - `mistral:7b` - Mistral 7B
   - `phi3:3.8b` - Microsoft Phi-3

2. Update `appsettings.json`:
   ```json
   "Ollama": {
     "Model": "llama3.2:3b",
     "BaseUrl": "http://172.17.0.2:11434"
   }
   ```

## Container Network Configuration

Your Ollama container is running in bridge network mode with:
- **Network**: bridge (default Docker network)
- **Internal Port**: 11434
- **Container IP**: 172.17.0.2
- **No port mapping to host**: The container port is not exposed to localhost

### If you want to access from localhost:

You can restart the container with port mapping:

```bash
# Stop and remove the existing container
docker stop quizzical_shannon
docker rm quizzical_shannon

# Run with port mapping
docker run -d -p 11434:11434 --name ollama_agent ollama/ollama

# Pull your model again
docker exec ollama_agent ollama pull qwen2.5:0.5b

# Update appsettings.json BaseUrl to:
# "BaseUrl": "http://localhost:11434"
```

## Available Models in Container

Check what models are available:
```bash
docker exec quizzical_shannon ollama list
```

Current output:
```
NAME              ID           SIZE      MODIFIED
qwen2.5:0.5b      (model id)   397 MB    (timestamp)
```

## Troubleshooting

### "Network error communicating with Ollama"
- Check container is running: `docker ps | grep quizzical_shannon`
- Verify IP address: `docker inspect quizzical_shannon --format "{{json .NetworkSettings.Networks.bridge.IPAddress}}"`
- Update `BaseUrl` in appsettings.json if IP changed

### "Model not found" error
- List available models: `docker exec quizzical_shannon ollama list`
- Pull the model: `docker exec quizzical_shannon ollama pull <model-name>`
- Verify model name matches exactly in appsettings.json

### Slow responses
- First response is slow while model loads into memory
- Subsequent responses should be faster
- Consider using a smaller model (0.5b or 3b parameters)
- Increase timeout in Program.cs if needed (currently 5 minutes)

### Container stopped or restarted
- Container IP might change after restart
- Check new IP: `docker inspect quizzical_shannon --format "{{json .NetworkSettings}}"`
- Update BaseUrl in appsettings.json

## Provider Capabilities

The OllamaProvider reports the following capabilities:
- **Tools Support**: Limited (depends on model)
- **Vision Support**: No (depends on model)
- **Streaming**: Yes (not yet implemented in provider)
- **Max Context Tokens**: 4096 (conservative default, varies by model)

## Token Usage

Unlike cloud providers, Ollama doesn't provide exact token counts in the API response. The provider:
- Estimates tokens using a simple character-based heuristic (4 chars ≈ 1 token)
- Reports `prompt_eval_count` and `eval_count` if available from Ollama
- Shows these as `input_tokens` and `output_tokens` for consistency

## Performance Notes

The `qwen2.5:0.5b` model:
- ✅ Very fast responses
- ✅ Small memory footprint (397 MB)
- ✅ Good for testing and development
- ⚠️ Limited capabilities compared to larger models
- ⚠️ May struggle with complex reasoning tasks

For production use, consider:
- `llama3.2:3b` - Good balance of speed and quality
- `qwen2.5:7b` - Better reasoning, still reasonably fast
- Cloud providers (Anthropic, OpenRouter) for critical tasks

## Next Steps

1. **Test the connection**: Run the CLI and verify it works
2. **Try different models**: Experiment with various Ollama models
3. **Build your use case**: Implement your detective agent logic
4. **Add tool calling**: When you reach Step 6, you can extend the provider to support tools (if the model supports it)
5. **Implement observability**: Add OpenTelemetry tracing (Step 2) to track performance

## Additional Resources

- [Ollama Model Library](https://ollama.com/library)
- [Ollama API Documentation](https://github.com/ollama/ollama/blob/main/docs/api.md)
- [Detective Agent Design](memory/DESIGN.md)
- [Implementation Plan](memory/PLAN.md)
- [Implementation Steps](memory/STEPS.md)
