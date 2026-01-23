namespace DetectiveAgent.Tools;

/// <summary>
/// Registry for managing available tools that the agent can use.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Register a new tool with the agent.
    /// </summary>
    void RegisterTool(ToolDefinition tool);

    /// <summary>
    /// Get all registered tools.
    /// </summary>
    IReadOnlyList<ToolDefinition> GetTools();

    /// <summary>
    /// Get a specific tool by name.
    /// </summary>
    ToolDefinition? GetTool(string name);

    /// <summary>
    /// Check if a tool with the given name is registered.
    /// </summary>
    bool HasTool(string name);
}
