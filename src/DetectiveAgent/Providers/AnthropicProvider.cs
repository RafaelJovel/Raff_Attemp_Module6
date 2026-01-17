namespace DetectiveAgent.Providers;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DetectiveAgent.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Anthropic Claude API provider implementation.
/// </summary>
public class AnthropicProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicProvider> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private const string ApiVersion = "2023-06-01";

    public AnthropicProvider(
        HttpClient httpClient,
        ILogger<AnthropicProvider> logger,
        string apiKey,
        string model = "claude-3-5-sonnet-20241022")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model;

        // Configure HttpClient
        if (string.IsNullOrEmpty(_httpClient.BaseAddress?.ToString()))
        {
            _httpClient.BaseAddress = new Uri("https://api.anthropic.com");
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
            // Separate system message from conversation messages
            var systemPrompt = messages.FirstOrDefault(m => m.Role == MessageRole.System)?.Content ?? string.Empty;
            var conversationMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

            var request = new AnthropicRequest
            {
                Model = _model,
                Messages = conversationMessages.Select(m => new AnthropicMessage
                {
                    Role = m.Role == MessageRole.User ? "user" : "assistant",
                    Content = m.Content
                }).ToList(),
                System = systemPrompt,
                MaxTokens = maxTokens ?? 4096,
                Temperature = temperature
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
            {
                Content = JsonContent.Create(request)
            };

            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", ApiVersion);

            _logger.LogInformation("Sending request to Anthropic API with model {Model}", _model);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Anthropic API error: {StatusCode} - {Content}", response.StatusCode, errorContent);

                throw response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                        new AuthenticationException($"Authentication failed: {errorContent}"),
                    System.Net.HttpStatusCode.TooManyRequests =>
                        new RateLimitException($"Rate limit exceeded: {errorContent}"),
                    System.Net.HttpStatusCode.BadRequest =>
                        new ValidationException($"Invalid request: {errorContent}"),
                    _ => new LlmProviderException($"API request failed: {errorContent}")
                };
            }

            var anthropicResponse = await response.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken);

            if (anthropicResponse == null || anthropicResponse.Content.Count == 0)
            {
                throw new LlmProviderException("Empty response from Anthropic API");
            }

            var messageContent = anthropicResponse.Content[0].Text;
            var metadata = new Dictionary<string, object>
            {
                ["model"] = _model,
                ["input_tokens"] = anthropicResponse.Usage.InputTokens,
                ["output_tokens"] = anthropicResponse.Usage.OutputTokens,
                ["stop_reason"] = anthropicResponse.StopReason ?? "unknown"
            };

            _logger.LogInformation("Received response from Anthropic: {InputTokens} input tokens, {OutputTokens} output tokens",
                anthropicResponse.Usage.InputTokens, anthropicResponse.Usage.OutputTokens);

            return new Message(
                MessageRole.Assistant,
                messageContent,
                DateTimeOffset.UtcNow,
                metadata);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error communicating with Anthropic API");
            throw new NetworkException("Network error communicating with Anthropic API", ex);
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
            SupportsTools: true,
            SupportsVision: true,
            SupportsStreaming: true,
            MaxContextTokens: 200_000 // Claude 3.5 Sonnet context window
        );
    }

    #region Anthropic API Models

    private class AnthropicRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required List<AnthropicMessage> Messages { get; set; }

        [JsonPropertyName("system")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? System { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? Temperature { get; set; }
    }

    private class AnthropicMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class AnthropicResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<ContentBlock> Content { get; set; } = new();

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("usage")]
        public required Usage Usage { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    #endregion
}
