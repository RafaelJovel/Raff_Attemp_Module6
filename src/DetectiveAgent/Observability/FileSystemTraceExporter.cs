using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace DetectiveAgent.Observability;

/// <summary>
/// Exports OpenTelemetry traces to the filesystem as JSON files.
/// Each trace is saved as a separate file in the configured traces directory.
/// </summary>
public class FileSystemTraceExporter : BaseExporter<Activity>
{
    private readonly string _tracesDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemTraceExporter(string tracesDirectory)
    {
        _tracesDirectory = tracesDirectory;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Ensure the traces directory exists
        Directory.CreateDirectory(_tracesDirectory);
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            foreach (var activity in batch)
            {
                ExportActivity(activity);
            }
            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error exporting traces: {ex.Message}");
            return ExportResult.Failure;
        }
    }

    private void ExportActivity(Activity activity)
    {
        var traceData = new
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            ParentSpanId = activity.ParentSpanId.ToString(),
            OperationName = activity.OperationName,
            DisplayName = activity.DisplayName,
            StartTime = activity.StartTimeUtc,
            Duration = activity.Duration,
            Status = activity.Status.ToString(),
            StatusDescription = activity.StatusDescription,
            Tags = activity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Events = activity.Events.Select(e => new
            {
                Name = e.Name,
                Timestamp = e.Timestamp,
                Tags = e.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString())
            }).ToList(),
            Resource = activity.Source.Name
        };

        // Use trace ID as the base filename, with span ID to make it unique
        var fileName = $"trace-{activity.TraceId}-{activity.SpanId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json";
        var filePath = Path.Combine(_tracesDirectory, fileName);

        var json = JsonSerializer.Serialize(traceData, _jsonOptions);
        File.WriteAllText(filePath, json);
    }
}
