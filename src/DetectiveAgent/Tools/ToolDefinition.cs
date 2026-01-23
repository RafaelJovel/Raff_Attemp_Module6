namespace DetectiveAgent.Tools;

using System.Text.Json;

/// <summary>
/// Represents a tool that the agent can use.
/// </summary>
public record ToolDefinition
{
    /// <summary>
    /// Unique identifier for this tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the tool does (used by LLM to decide when to call it).
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON schema defining the expected parameters for this tool.
    /// </summary>
    public required JsonDocument ParametersSchema { get; init; }

    /// <summary>
    /// Function that executes the tool logic.
    /// </summary>
    public required Func<JsonDocument, Task<ToolResult>> Handler { get; init; }
}
