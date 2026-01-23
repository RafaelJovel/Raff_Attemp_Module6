namespace DetectiveAgent.Tools;

/// <summary>
/// Exception thrown when tool execution fails.
/// </summary>
public class ToolExecutionException : Exception
{
    public string ToolName { get; }

    public ToolExecutionException(string toolName, string message)
        : base(message)
    {
        ToolName = toolName;
    }

    public ToolExecutionException(string toolName, string message, Exception innerException)
        : base(message, innerException)
    {
        ToolName = toolName;
    }
}
