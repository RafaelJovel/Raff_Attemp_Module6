namespace DetectiveAgent.Providers;

/// <summary>
/// Base exception for LLM provider errors.
/// </summary>
public class LlmProviderException : Exception
{
    public LlmProviderException(string message) : base(message) { }
    public LlmProviderException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when provider rate limits are exceeded.
/// </summary>
public class RateLimitException : LlmProviderException
{
    public TimeSpan? RetryAfter { get; init; }

    public RateLimitException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationException : LlmProviderException
{
    public AuthenticationException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when request validation fails.
/// </summary>
public class ValidationException : LlmProviderException
{
    public ValidationException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown for network-related errors.
/// </summary>
public class NetworkException : LlmProviderException
{
    public NetworkException(string message, Exception inner) : base(message, inner) { }
}
