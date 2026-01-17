namespace DetectiveAgent.Retry;

using System.Diagnostics;
using System.Net;
using DetectiveAgent.Observability;
using DetectiveAgent.Providers;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

/// <summary>
/// Provides Polly-based retry policies for HTTP requests with exponential backoff and jitter.
/// </summary>
public static class PollyRetryPolicy
{
    /// <summary>
    /// Creates a retry policy for handling transient HTTP failures.
    /// </summary>
    /// <param name="config">Retry configuration settings.</param>
    /// <param name="logger">Logger for retry events.</param>
    /// <returns>An async retry policy for HttpResponseMessage.</returns>
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(
        RetryConfiguration config,
        ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles 5xx and 408
            .Or<NetworkException>()
            .OrResult(response => ShouldRetry(response))
            .WaitAndRetryAsync(
                retryCount: config.MaxAttempts,
                sleepDurationProvider: (retryAttempt, result, context) =>
                    GetBackoffDuration(retryAttempt, config, result),
                onRetryAsync: async (outcome, timespan, retryAttempt, context) =>
                    await OnRetryAsync(outcome, timespan, retryAttempt, context, logger));
    }

    /// <summary>
    /// Determines if a response should trigger a retry.
    /// </summary>
    private static bool ShouldRetry(HttpResponseMessage response)
    {
        // Retry on rate limits
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return true;

        // Retry on server errors
        if (response.StatusCode == HttpStatusCode.InternalServerError ||
            response.StatusCode == HttpStatusCode.BadGateway ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable ||
            response.StatusCode == HttpStatusCode.GatewayTimeout)
            return true;

        // Don't retry on client errors (except those already handled above)
        return false;
    }

    /// <summary>
    /// Calculates backoff duration with exponential backoff and optional jitter.
    /// </summary>
    private static TimeSpan GetBackoffDuration(
        int retryAttempt,
        RetryConfiguration config,
        DelegateResult<HttpResponseMessage> result)
    {
        // Check if response has Retry-After header
        if (result.Result?.Headers.RetryAfter != null)
        {
            if (result.Result.Headers.RetryAfter.Delta.HasValue)
            {
                var retryAfter = result.Result.Headers.RetryAfter.Delta.Value;
                return TimeSpan.FromMilliseconds(
                    Math.Min(retryAfter.TotalMilliseconds, config.MaxDelayMs));
            }
        }

        // Calculate exponential backoff
        var exponentialDelay = config.InitialDelayMs * Math.Pow(config.BackoffFactor, retryAttempt - 1);
        
        // Cap at max delay
        var delayMs = Math.Min(exponentialDelay, config.MaxDelayMs);

        // Add jitter if enabled
        if (config.UseJitter)
        {
            var jitter = Random.Shared.NextDouble() * 0.3; // Up to 30% jitter
            delayMs = delayMs * (1 + jitter);
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Handles retry events with logging and tracing.
    /// </summary>
    private static Task OnRetryAsync(
        DelegateResult<HttpResponseMessage> outcome,
        TimeSpan timespan,
        int retryAttempt,
        Context context,
        ILogger logger)
    {
        var statusCode = outcome.Result?.StatusCode.ToString() ?? "Unknown";
        var exception = outcome.Exception?.Message ?? "None";

        logger.LogWarning(
            "Retry attempt {RetryAttempt} after {DelayMs}ms. Status: {StatusCode}, Exception: {Exception}",
            retryAttempt,
            timespan.TotalMilliseconds,
            statusCode,
            exception);

        // Add retry information to current activity/span
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag($"retry.attempt_{retryAttempt}.status", statusCode);
            activity.SetTag($"retry.attempt_{retryAttempt}.delay_ms", timespan.TotalMilliseconds);
            
            if (outcome.Exception != null)
            {
                activity.SetTag($"retry.attempt_{retryAttempt}.exception", outcome.Exception.GetType().Name);
            }

            // Add event for retry
            activity.AddEvent(new ActivityEvent(
                "Retry",
                DateTimeOffset.UtcNow,
                new ActivityTagsCollection
                {
                    { "retry.attempt", retryAttempt },
                    { "retry.delay_ms", timespan.TotalMilliseconds },
                    { "retry.status", statusCode }
                }));
        }

        return Task.CompletedTask;
    }
}
