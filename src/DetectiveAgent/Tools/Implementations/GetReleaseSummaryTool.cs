namespace DetectiveAgent.Tools.Implementations;

using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool for retrieving release summary information.
/// Returns mock release data for demonstration purposes.
/// </summary>
public class GetReleaseSummaryTool
{
    private readonly ILogger<GetReleaseSummaryTool> _logger;

    public GetReleaseSummaryTool(ILogger<GetReleaseSummaryTool> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a ToolDefinition for the Get Release Summary tool.
    /// </summary>
    public static ToolDefinition CreateDefinition(ILogger<GetReleaseSummaryTool> logger)
    {
        var tool = new GetReleaseSummaryTool(logger);

        var parametersSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""release_id"": {
                    ""type"": ""string"",
                    ""description"": ""The ID or version of the release to retrieve""
                }
            },
            ""required"": [""release_id""]
        }");

        return new ToolDefinition
        {
            Name = "get_release_summary",
            Description = "Retrieves high-level information about a software release including version, changes, test results, and deployment metrics.",
            ParametersSchema = parametersSchema,
            Handler = tool.ExecuteAsync
        };
    }

    private async Task<ToolResult> ExecuteAsync(JsonDocument arguments)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Parse arguments
            if (!arguments.RootElement.TryGetProperty("release_id", out var releaseIdElement))
            {
                return new ToolResult
                {
                    ToolCallId = Guid.NewGuid().ToString(),
                    Content = "Error: release_id parameter is required",
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            var releaseId = releaseIdElement.GetString();
            _logger.LogInformation("Fetching release summary for: {ReleaseId}", releaseId);

            // Mock release data - in a real implementation, this would call an actual API
            var releaseSummary = GetMockReleaseSummary(releaseId);

            var resultJson = JsonSerializer.Serialize(releaseSummary, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return new ToolResult
            {
                ToolCallId = Guid.NewGuid().ToString(),
                Content = resultJson,
                Success = true,
                Timestamp = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["release_id"] = releaseId ?? "unknown"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching release summary");
            return new ToolResult
            {
                ToolCallId = Guid.NewGuid().ToString(),
                Content = $"Error: {ex.Message}",
                Success = false,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }

    private static object GetMockReleaseSummary(string? releaseId)
    {
        // Return different scenarios based on release ID
        return releaseId?.ToLowerInvariant() switch
        {
            "v2.1.0" or "high-risk" => new
            {
                version = "v2.1.0",
                changes = new[]
                {
                    "Added payment processing functionality",
                    "Updated authentication service",
                    "Database schema migration for user profiles"
                },
                tests = new
                {
                    passed = 140,
                    failed = 5,
                    skipped = 3
                },
                deployment_metrics = new
                {
                    error_rate = 0.08,
                    response_time_p95 = 850,
                    deployment_duration_minutes = 45
                }
            },
            "v2.0.5" or "medium-risk" => new
            {
                version = "v2.0.5",
                changes = new[]
                {
                    "Fixed logging bug in order service",
                    "Updated dependency versions",
                    "Minor UI improvements"
                },
                tests = new
                {
                    passed = 145,
                    failed = 2,
                    skipped = 1
                },
                deployment_metrics = new
                {
                    error_rate = 0.03,
                    response_time_p95 = 520,
                    deployment_duration_minutes = 30
                }
            },
            "v2.0.4" or "low-risk" => new
            {
                version = "v2.0.4",
                changes = new[]
                {
                    "Documentation updates",
                    "Code cleanup and refactoring",
                    "Updated README"
                },
                tests = new
                {
                    passed = 148,
                    failed = 0,
                    skipped = 0
                },
                deployment_metrics = new
                {
                    error_rate = 0.01,
                    response_time_p95 = 380,
                    deployment_duration_minutes = 25
                }
            },
            _ => new
            {
                version = releaseId ?? "unknown",
                changes = new[]
                {
                    "General bug fixes",
                    "Performance improvements"
                },
                tests = new
                {
                    passed = 142,
                    failed = 2,
                    skipped = 2
                },
                deployment_metrics = new
                {
                    error_rate = 0.02,
                    response_time_p95 = 450,
                    deployment_duration_minutes = 35
                }
            }
        };
    }
}
