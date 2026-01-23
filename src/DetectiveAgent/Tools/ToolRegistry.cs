namespace DetectiveAgent.Tools;

using Microsoft.Extensions.Logging;

/// <summary>
/// Default implementation of IToolRegistry.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ToolDefinition> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(ILogger<ToolRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterTool(ToolDefinition tool)
    {
        if (tool == null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            throw new ArgumentException("Tool name cannot be empty", nameof(tool));
        }

        if (_tools.ContainsKey(tool.Name))
        {
            _logger.LogWarning("Tool {ToolName} is already registered. Overwriting.", tool.Name);
        }

        _tools[tool.Name] = tool;
        _logger.LogInformation("Registered tool: {ToolName}", tool.Name);
    }

    public IReadOnlyList<ToolDefinition> GetTools()
    {
        return _tools.Values.ToList();
    }

    public ToolDefinition? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    public bool HasTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _tools.ContainsKey(name);
    }
}
