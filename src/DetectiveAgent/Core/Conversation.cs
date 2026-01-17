namespace DetectiveAgent.Core;

/// <summary>
/// Represents an ongoing dialogue between user and agent.
/// </summary>
public record Conversation
{
    public required string Id { get; init; }
    public required string SystemPrompt { get; init; }
    public required List<Message> Messages { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// The OpenTelemetry trace ID associated with this conversation.
    /// Used to correlate conversation data with trace files.
    /// </summary>
    public string? TraceId { get; init; }
}
