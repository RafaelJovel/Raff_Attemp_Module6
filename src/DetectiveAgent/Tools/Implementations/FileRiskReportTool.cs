namespace DetectiveAgent.Tools.Implementations;

using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tool for filing risk assessment reports.
/// Stores risk reports for demonstration purposes.
/// </summary>
public class FileRiskReportTool
{
    private readonly ILogger<FileRiskReportTool> _logger;
    private readonly string _reportsPath;

    public FileRiskReportTool(ILogger<FileRiskReportTool> logger, string? reportsPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reportsPath = reportsPath ?? Path.Combine("data", "risk-reports");
    }

    /// <summary>
    /// Creates a ToolDefinition for the File Risk Report tool.
    /// </summary>
    public static ToolDefinition CreateDefinition(ILogger<FileRiskReportTool> logger, string? reportsPath = null)
    {
        var tool = new FileRiskReportTool(logger, reportsPath);

        var parametersSchema = JsonDocument.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""release_id"": {
                    ""type"": ""string"",
                    ""description"": ""The ID or version of the release being assessed""
                },
                ""severity"": {
                    ""type"": ""string"",
                    ""enum"": [""low"", ""medium"", ""high"", ""critical""],
                    ""description"": ""The risk severity level""
                },
                ""findings"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""type"": ""string""
                    },
                    ""description"": ""List of risk findings or concerns identified""
                }
            },
            ""required"": [""release_id"", ""severity"", ""findings""]
        }");

        return new ToolDefinition
        {
            Name = "file_risk_report",
            Description = "Files a risk assessment report for a software release with severity level and identified concerns.",
            ParametersSchema = parametersSchema,
            Handler = tool.ExecuteAsync
        };
    }

    private async Task<ToolResult> ExecuteAsync(JsonDocument arguments)
    {
        try
        {
            // Parse arguments
            var root = arguments.RootElement;

            if (!root.TryGetProperty("release_id", out var releaseIdElement))
            {
                return new ToolResult
                {
                    ToolCallId = Guid.NewGuid().ToString(),
                    Content = "Error: release_id parameter is required",
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            if (!root.TryGetProperty("severity", out var severityElement))
            {
                return new ToolResult
                {
                    ToolCallId = Guid.NewGuid().ToString(),
                    Content = "Error: severity parameter is required",
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            if (!root.TryGetProperty("findings", out var findingsElement))
            {
                return new ToolResult
                {
                    ToolCallId = Guid.NewGuid().ToString(),
                    Content = "Error: findings parameter is required",
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            var releaseId = releaseIdElement.GetString();
            var severity = severityElement.GetString();

            // Validate severity
            var validSeverities = new[] { "low", "medium", "high", "critical" };
            if (!validSeverities.Contains(severity?.ToLowerInvariant()))
            {
                return new ToolResult
                {
                    ToolCallId = Guid.NewGuid().ToString(),
                    Content = $"Error: severity must be one of: {string.Join(", ", validSeverities)}",
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            // Parse findings array
            var findings = new List<string>();
            foreach (var finding in findingsElement.EnumerateArray())
            {
                var findingText = finding.GetString();
                if (!string.IsNullOrWhiteSpace(findingText))
                {
                    findings.Add(findingText);
                }
            }

            if (findings.Count == 0)
            {
                return new ToolResult
                {
                    ToolCallId = Guid.NewGuid().ToString(),
                    Content = "Error: findings array must contain at least one finding",
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            // Create risk report
            var reportId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var report = new
            {
                report_id = reportId,
                release_id = releaseId,
                severity = severity?.ToLowerInvariant(),
                findings = findings,
                filed_at = DateTimeOffset.UtcNow,
                status = "filed"
            };

            // Save report to file
            Directory.CreateDirectory(_reportsPath);
            var reportPath = Path.Combine(_reportsPath, $"report_{reportId}.json");
            var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(reportPath, reportJson);

            _logger.LogInformation("Filed risk report {ReportId} for release {ReleaseId} with severity {Severity}",
                reportId, releaseId, severity);

            var resultJson = JsonSerializer.Serialize(new
            {
                report_id = reportId,
                status = "success",
                message = $"Risk report filed successfully for release {releaseId}",
                severity = severity?.ToLowerInvariant(),
                findings_count = findings.Count
            }, new JsonSerializerOptions
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
                    ["report_id"] = reportId,
                    ["release_id"] = releaseId ?? "unknown",
                    ["severity"] = severity ?? "unknown",
                    ["findings_count"] = findings.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filing risk report");
            return new ToolResult
            {
                ToolCallId = Guid.NewGuid().ToString(),
                Content = $"Error: {ex.Message}",
                Success = false,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }
}
