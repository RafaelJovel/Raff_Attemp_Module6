namespace DetectiveAgent.Context;

using DetectiveAgent.Core;
using DetectiveAgent.Providers;

/// <summary>
/// Manages context window to ensure conversations fit within model token limits.
/// Implements truncation strategy to keep conversations within bounds.
/// </summary>
public class ContextWindowManager
{
    private const float TokenLimitThreshold = 0.9f; // Truncate at 90% of limit
    private const float BufferPercent = 0.1f; // Reserve 10% safety margin

    /// <summary>
    /// Prepare messages for provider call, truncating if necessary to fit within token limits.
    /// </summary>
    /// <param name="systemPrompt">System prompt (always preserved)</param>
    /// <param name="messages">Conversation messages</param>
    /// <param name="provider">LLM provider for token estimation and limits</param>
    /// <param name="maxTokens">Max tokens for response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Managed context with messages that fit within limits and metadata about truncation</returns>
    public async Task<ManagedContext> ManageContextAsync(
        string systemPrompt,
        IReadOnlyList<Message> messages,
        ILlmProvider provider,
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        var capabilities = provider.GetCapabilities();
        var maxContextTokens = capabilities.MaxContextTokens;

        // Calculate token budget
        var systemMessage = new Message(MessageRole.System, systemPrompt, DateTimeOffset.UtcNow);
        var systemTokens = await provider.EstimateTokensAsync(
            new[] { systemMessage }, 
            cancellationToken);

        // Reserve tokens: system prompt + response + safety buffer
        var reservedTokens = systemTokens + maxTokens;
        var bufferTokens = (int)(maxContextTokens * BufferPercent);
        var availableForHistory = maxContextTokens - reservedTokens - bufferTokens;

        if (availableForHistory <= 0)
        {
            throw new InvalidOperationException(
                $"System prompt and max_tokens exceed context window. " +
                $"System: {systemTokens}, Max tokens: {maxTokens}, " +
                $"Context limit: {maxContextTokens}");
        }

        // Build message list with system prompt first
        var allMessages = new List<Message> { systemMessage };
        allMessages.AddRange(messages);

        // Estimate current token usage
        var currentTokens = await provider.EstimateTokensAsync(allMessages, cancellationToken);
        var totalBudget = reservedTokens + availableForHistory;

        // Check if truncation is needed (at 90% threshold)
        var truncationThreshold = (int)(totalBudget * TokenLimitThreshold);
        bool needsTruncation = currentTokens > truncationThreshold;

        List<Message> managedMessages;
        int messagesRemoved = 0;

        if (needsTruncation)
        {
            // Keep system prompt + most recent messages that fit
            managedMessages = await TruncateMessagesAsync(
                systemMessage,
                messages,
                availableForHistory,
                provider,
                cancellationToken);

            messagesRemoved = messages.Count - (managedMessages.Count - 1); // -1 for system message
            currentTokens = await provider.EstimateTokensAsync(managedMessages, cancellationToken);
        }
        else
        {
            managedMessages = allMessages;
        }

        return new ManagedContext(
            Messages: managedMessages,
            EstimatedTokens: currentTokens,
            MaxContextTokens: maxContextTokens,
            AvailableTokens: maxContextTokens - currentTokens,
            Utilization: (float)currentTokens / maxContextTokens,
            WasTruncated: needsTruncation,
            MessagesRemoved: messagesRemoved,
            TokenBudget: new TokenBudget(
                SystemPrompt: systemTokens,
                MaxResponse: maxTokens,
                SafetyBuffer: bufferTokens,
                HistoryAvailable: availableForHistory,
                Total: maxContextTokens
            )
        );
    }

    /// <summary>
    /// Truncate messages to fit within available token budget.
    /// Strategy: Keep system prompt + most recent N messages that fit.
    /// </summary>
    private async Task<List<Message>> TruncateMessagesAsync(
        Message systemMessage,
        IReadOnlyList<Message> messages,
        int availableTokens,
        ILlmProvider provider,
        CancellationToken cancellationToken)
    {
        var result = new List<Message> { systemMessage };
        
        // Start from most recent and work backwards
        var recentMessages = new List<Message>();
        int estimatedTokens = 0;

        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var testMessages = new List<Message> { messages[i] };
            testMessages.AddRange(recentMessages);

            var tokens = await provider.EstimateTokensAsync(testMessages, cancellationToken);

            if (tokens <= availableTokens)
            {
                recentMessages.Insert(0, messages[i]);
                estimatedTokens = tokens;
            }
            else
            {
                // Would exceed budget, stop here
                break;
            }
        }

        result.AddRange(recentMessages);
        return result;
    }
}

/// <summary>
/// Result of context window management with metadata about the managed context.
/// </summary>
public record ManagedContext(
    List<Message> Messages,
    int EstimatedTokens,
    int MaxContextTokens,
    int AvailableTokens,
    float Utilization,
    bool WasTruncated,
    int MessagesRemoved,
    TokenBudget TokenBudget
);

/// <summary>
/// Breakdown of token budget allocation.
/// </summary>
public record TokenBudget(
    int SystemPrompt,
    int MaxResponse,
    int SafetyBuffer,
    int HistoryAvailable,
    int Total
);
