namespace DetectiveAgent.Tools;

/// <summary>
/// Represents the outcome of executing a tool.
/// </summary>
public record ToolResult
{
    /// <summary>
    /// Links back to the originating tool call.
    /// </summary>
    public required string ToolCallId { get; init; }

    /// <summary>
    /// The result content (success data or error message).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Indicates whether the tool execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// When execution completed.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Additional metadata about execution (execution time, error details, etc.).
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
