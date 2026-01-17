using System.Diagnostics;

namespace DetectiveAgent.Observability;

/// <summary>
/// Provides a centralized ActivitySource for the Detective Agent.
/// All agent operations should create spans using this source.
/// </summary>
public static class AgentActivitySource
{
    /// <summary>
    /// The name of the activity source, used for filtering traces.
    /// </summary>
    public const string SourceName = "DetectiveAgent";

    /// <summary>
    /// The version of the activity source.
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// The shared ActivitySource instance for creating traces and spans.
    /// </summary>
    public static readonly ActivitySource Instance = new(SourceName, Version);
}
