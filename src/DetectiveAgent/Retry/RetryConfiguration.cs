namespace DetectiveAgent.Retry;

/// <summary>
/// Configuration for retry behavior with exponential backoff.
/// </summary>
public class RetryConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry in milliseconds. Default is 1000ms (1 second).
    /// </summary>
    public int InitialDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay between retries in milliseconds. Default is 60000ms (60 seconds).
    /// </summary>
    public int MaxDelayMs { get; set; } = 60000;

    /// <summary>
    /// Backoff multiplier for exponential backoff. Default is 2.0.
    /// </summary>
    public double BackoffFactor { get; set; } = 2.0;

    /// <summary>
    /// Whether to add jitter to backoff delays. Default is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;
}
