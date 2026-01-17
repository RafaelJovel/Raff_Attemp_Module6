namespace DetectiveAgent.Providers;

using DetectiveAgent.Core;

/// <summary>
/// Abstraction for LLM providers to enable provider-agnostic agent implementation.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Send messages to LLM and get assistant's response.
    /// </summary>
    Task<Message> CompleteAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default,
        float? temperature = null,
        int? maxTokens = null);

    /// <summary>
    /// Estimate token count for messages (used for context window management).
    /// </summary>
    Task<int> EstimateTokensAsync(
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get capabilities of this provider.
    /// </summary>
    ProviderCapabilities GetCapabilities();
}

/// <summary>
/// Describes what capabilities a provider supports.
/// </summary>
public record ProviderCapabilities(
    bool SupportsTools,
    bool SupportsVision,
    bool SupportsStreaming,
    int MaxContextTokens
);
