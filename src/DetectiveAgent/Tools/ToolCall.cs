namespace DetectiveAgent.Tools;

using System.Text.Json;

/// <summary>
/// Represents a request from the LLM to execute a tool.
/// </summary>
public record ToolCall
{
    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name of the tool to execute.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// JSON object with tool parameters.
    /// </summary>
    public required JsonDocument Arguments { get; init; }

    /// <summary>
    /// When the tool call was requested.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}
