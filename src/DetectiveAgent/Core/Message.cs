namespace DetectiveAgent.Core;

/// <summary>
/// Represents a single message in a conversation.
/// </summary>
public record Message(
    MessageRole Role,
    string Content,
    DateTimeOffset Timestamp,
    Dictionary<string, object>? Metadata = null
);

/// <summary>
/// Defines the role of a message sender.
/// </summary>
public enum MessageRole
{
    User,
    Assistant,
    System
}
