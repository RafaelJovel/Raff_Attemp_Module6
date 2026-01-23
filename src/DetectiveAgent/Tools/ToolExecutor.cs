namespace DetectiveAgent.Tools;

using System.Diagnostics;
using DetectiveAgent.Observability;
using Microsoft.Extensions.Logging;

/// <summary>
/// Executes tool calls with proper error handling and observability.
/// </summary>
public class ToolExecutor
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(IToolRegistry toolRegistry, ILogger<ToolExecutor> logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute a tool call and return the result.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(
        ToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        using var activity = AgentActivitySource.Instance.StartActivity(
            "ToolExecutor.Execute",
            ActivityKind.Internal);

        activity?.SetTag("tool.name", toolCall.Name);
        activity?.SetTag("tool.call_id", toolCall.Id);

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Get the tool definition
            var tool = _toolRegistry.GetTool(toolCall.Name);
            if (tool == null)
            {
                var errorMessage = $"Tool '{toolCall.Name}' not found in registry";
                _logger.LogError(errorMessage);
                activity?.SetStatus(ActivityStatusCode.Error, errorMessage);

                return new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    Content = errorMessage,
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = "ToolNotFound",
                        ["execution_time_ms"] = 0
                    }
                };
            }

            _logger.LogInformation("Executing tool: {ToolName}", toolCall.Name);

            // Execute the tool handler
            var result = await tool.Handler(toolCall.Arguments);

            var executionTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            activity?.SetTag("tool.success", result.Success);
            activity?.SetTag("tool.execution_time_ms", executionTime);

            // Add execution time to result metadata
            if (result.Metadata == null)
            {
                result = result with
                {
                    Metadata = new Dictionary<string, object>
                    {
                        ["execution_time_ms"] = executionTime
                    }
                };
            }
            else if (!result.Metadata.ContainsKey("execution_time_ms"))
            {
                result.Metadata["execution_time_ms"] = executionTime;
            }

            if (result.Success)
            {
                _logger.LogInformation("Tool {ToolName} executed successfully in {ExecutionTime}ms",
                    toolCall.Name, executionTime);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                _logger.LogWarning("Tool {ToolName} execution failed: {Error}",
                    toolCall.Name, result.Content);
                activity?.SetStatus(ActivityStatusCode.Error, result.Content);
            }

            return result;
        }
        catch (Exception ex)
        {
            var executionTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            var errorMessage = $"Tool execution failed: {ex.Message}";

            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new ToolResult
            {
                ToolCallId = toolCall.Id,
                Content = errorMessage,
                Success = false,
                Timestamp = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["error"] = ex.GetType().Name,
                    ["error_message"] = ex.Message,
                    ["execution_time_ms"] = executionTime
                }
            };
        }
    }
}
