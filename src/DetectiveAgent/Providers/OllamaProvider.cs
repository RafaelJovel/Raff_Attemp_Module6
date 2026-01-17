namespace DetectiveAgent.Providers;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DetectiveAgent.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Ollama provider implementation using OpenAI-compatible API.
/// </summary>
public class OllamaProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaProvider> _logger;
    private readonly string _model;

    public OllamaProvider(
        HttpClient httpClient,
        ILogger<OllamaProvider> logger,
        string model = "llama2")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = model;

        // Configure HttpClient for local Ollama
        if (string.IsNullOrEmpty(_httpClient.BaseAddress?.ToString()))
        {
            _httpClient.BaseAddress = new Uri("http://localhost:11434");
        }
    }

    public async Task<Message> CompleteAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default,
        float? temperature = null,
        int? maxTokens = null)
    {
        try
        {
            // Convert messages to OpenAI format that Ollama supports
            var ollamaMessages = messages.Select(m => new OllamaMessage
            {
                Role = m.Role switch
                {
                    MessageRole.System => "system",
                    MessageRole.User => "user",
                    MessageRole.Assistant => "assistant",
                    _ => "user"
                },
                Content = m.Content
            }).ToList();

            var request = new OllamaRequest
            {
                Model = _model,
                Messages = ollamaMessages,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = temperature,
                    NumPredict = maxTokens
                }
            };

            _logger.LogInformation("Sending request to Ollama with model {Model}", _model);

            var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Ollama API error: {StatusCode} - {Content}", response.StatusCode, errorContent);

                throw response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest =>
                        new ValidationException($"Invalid request: {errorContent}"),
                    System.Net.HttpStatusCode.NotFound =>
                        new ValidationException($"Model '{_model}' not found. Pull it first with: ollama pull {_model}"),
                    _ => new LlmProviderException($"API request failed: {errorContent}")
                };
            }

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken);

            if (ollamaResponse == null || ollamaResponse.Message == null)
            {
                throw new LlmProviderException("Empty response from Ollama API");
            }

            var messageContent = ollamaResponse.Message.Content;
            var metadata = new Dictionary<string, object>
            {
                ["model"] = _model,
                ["created_at"] = ollamaResponse.CreatedAt ?? DateTimeOffset.UtcNow.ToString("o"),
                ["done"] = ollamaResponse.Done,
                ["total_duration"] = ollamaResponse.TotalDuration ?? 0,
                ["prompt_eval_count"] = ollamaResponse.PromptEvalCount ?? 0,
                ["eval_count"] = ollamaResponse.EvalCount ?? 0
            };

            // Add token counts if available for compatibility
            if (ollamaResponse.PromptEvalCount.HasValue)
            {
                metadata["input_tokens"] = ollamaResponse.PromptEvalCount.Value;
            }
            if (ollamaResponse.EvalCount.HasValue)
            {
                metadata["output_tokens"] = ollamaResponse.EvalCount.Value;
            }

            _logger.LogInformation("Received response from Ollama: {PromptTokens} prompt tokens, {CompletionTokens} completion tokens",
                ollamaResponse.PromptEvalCount ?? 0, ollamaResponse.EvalCount ?? 0);

            return new Message(
                MessageRole.Assistant,
                messageContent,
                DateTimeOffset.UtcNow,
                metadata);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with Ollama. Is the container running?");
            throw new NetworkException("Network error communicating with Ollama. Ensure container is running and accessible.", ex);
        }
    }

    public Task<int> EstimateTokensAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        // Simple estimation: ~4 characters per token (rough approximation)
        var totalChars = messages.Sum(m => m.Content.Length);
        var estimatedTokens = totalChars / 4;

        return Task.FromResult(estimatedTokens);
    }

    public ProviderCapabilities GetCapabilities()
    {
        return new ProviderCapabilities(
            SupportsTools: false, // Ollama has limited tool support depending on model
            SupportsVision: false, // Depends on model
            SupportsStreaming: true,
            MaxContextTokens: 4096 // Varies by model, this is a conservative default
        );
    }

    #region Ollama API Models

    private class OllamaRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required List<OllamaMessage> Messages { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OllamaOptions? Options { get; set; }
    }

    private class OllamaMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? Temperature { get; set; }

        [JsonPropertyName("num_predict")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? NumPredict { get; set; }
    }

    private class OllamaResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; set; }

        [JsonPropertyName("load_duration")]
        public long? LoadDuration { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }

        [JsonPropertyName("eval_duration")]
        public long? EvalDuration { get; set; }
    }

    #endregion
}
