namespace DetectiveAgent.Core;

using System.Diagnostics;
using DetectiveAgent.Observability;
using DetectiveAgent.Providers;
using DetectiveAgent.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// The Detective Agent - orchestrates conversation with LLM provider.
/// </summary>
public class Agent
{
    private readonly ILlmProvider _provider;
    private readonly IConversationStore _store;
    private readonly ILogger<Agent> _logger;
    private Conversation? _currentConversation;
    private readonly float _defaultTemperature;
    private readonly int _defaultMaxTokens;
    private readonly string _systemPrompt;

    public Agent(
        ILlmProvider provider,
        IConversationStore store,
        ILogger<Agent> logger,
        string systemPrompt = "You are a helpful AI assistant.",
        float defaultTemperature = 0.7f,
        int defaultMaxTokens = 4096)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultTemperature = defaultTemperature;
        _defaultMaxTokens = defaultMaxTokens;
        _systemPrompt = systemPrompt;

        // Don't create conversation here - will be lazily created on first use
        // This ensures OpenTelemetry is initialized before creating activities
    }

    /// <summary>
    /// Send a message to the agent and get a response.
    /// </summary>
    public async Task<Message> SendMessageAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        using var activity = AgentActivitySource.Instance.StartActivity(
            "Agent.SendMessage",
            ActivityKind.Internal);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty", nameof(content));
        }

        // Lazy initialization: create conversation on first use if it doesn't exist
        // This ensures OpenTelemetry is initialized before creating activities
        if (_currentConversation == null)
        {
            StartNewConversation(_systemPrompt);
        }

        // Capture trace ID if not already set (update with current activity's trace)
        if (string.IsNullOrEmpty(_currentConversation!.TraceId) && activity != null)
        {
            _currentConversation = _currentConversation with 
            { 
                TraceId = activity.TraceId.ToString() 
            };
        }

        // Add trace tags
        activity?.SetTag("conversation.id", _currentConversation.Id);
        activity?.SetTag("message.length", content.Length);
        activity?.SetTag("message.count", _currentConversation.Messages.Count + 1);

        _logger.LogInformation("Sending user message: {MessagePreview}...", 
            content.Length > 50 ? content.Substring(0, 50) : content);

        // Add user message to conversation
        var userMessage = new Message(
            MessageRole.User,
            content,
            DateTimeOffset.UtcNow);

        _currentConversation.Messages.Add(userMessage);

        try
        {
            // Get all messages including system prompt
            var allMessages = new List<Message>
            {
                new Message(MessageRole.System, _currentConversation.SystemPrompt, DateTimeOffset.UtcNow)
            };
            allMessages.AddRange(_currentConversation.Messages);

            // Call LLM provider
            var assistantMessage = await _provider.CompleteAsync(
                allMessages,
                cancellationToken,
                _defaultTemperature,
                _defaultMaxTokens);

            // Add assistant response to conversation
            _currentConversation.Messages.Add(assistantMessage);

            // Extract token information from metadata
            int inputTokens = 0;
            int outputTokens = 0;
            
            if (assistantMessage.Metadata != null)
            {
                if (assistantMessage.Metadata.TryGetValue("inputTokens", out var inputTokensObj))
                {
                    inputTokens = Convert.ToInt32(inputTokensObj);
                    activity?.SetTag("tokens.input", inputTokens);
                }
                else if (assistantMessage.Metadata.TryGetValue("input_tokens", out var inputTokensObj2))
                {
                    inputTokens = Convert.ToInt32(inputTokensObj2);
                    activity?.SetTag("tokens.input", inputTokens);
                }
                
                if (assistantMessage.Metadata.TryGetValue("outputTokens", out var outputTokensObj))
                {
                    outputTokens = Convert.ToInt32(outputTokensObj);
                    activity?.SetTag("tokens.output", outputTokens);
                }
                else if (assistantMessage.Metadata.TryGetValue("output_tokens", out var outputTokensObj2))
                {
                    outputTokens = Convert.ToInt32(outputTokensObj2);
                    activity?.SetTag("tokens.output", outputTokens);
                }
                
                if (assistantMessage.Metadata.TryGetValue("totalTokens", out var totalTokensObj))
                {
                    activity?.SetTag("tokens.total", totalTokensObj);
                }
                else if (inputTokens > 0 || outputTokens > 0)
                {
                    activity?.SetTag("tokens.total", inputTokens + outputTokens);
                }
            }

            activity?.SetTag("response.length", assistantMessage.Content.Length);

            // Update metadata
            if (_currentConversation.Metadata == null)
            {
                _currentConversation = _currentConversation with 
                { 
                    Metadata = new Dictionary<string, object>() 
                };
            }
            
            _currentConversation.Metadata["lastUpdated"] = DateTimeOffset.UtcNow;
            _currentConversation.Metadata["messageCount"] = _currentConversation.Messages.Count;
            
            // Track cumulative token usage at conversation level
            if (inputTokens > 0 || outputTokens > 0)
            {
                var totalInput = Convert.ToInt32(_currentConversation.Metadata.GetValueOrDefault("totalInputTokens", 0)) + inputTokens;
                var totalOutput = Convert.ToInt32(_currentConversation.Metadata.GetValueOrDefault("totalOutputTokens", 0)) + outputTokens;
                
                _currentConversation.Metadata["totalInputTokens"] = totalInput;
                _currentConversation.Metadata["totalOutputTokens"] = totalOutput;
                _currentConversation.Metadata["totalTokens"] = totalInput + totalOutput;
                
                activity?.SetTag("conversation.total_tokens", totalInput + totalOutput);
            }

            // Save conversation
            await _store.SaveAsync(_currentConversation, cancellationToken);

            _logger.LogInformation("Received assistant response: {MessagePreview}...",
                assistantMessage.Content.Length > 50 
                    ? assistantMessage.Content.Substring(0, 50) 
                    : assistantMessage.Content);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return assistantMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during message exchange");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Start a new conversation, saving the current one if it exists.
    /// </summary>
    public void StartNewConversation(string? systemPrompt = null)
    {
        using var activity = AgentActivitySource.Instance.StartActivity(
            "Agent.StartNewConversation",
            ActivityKind.Internal);
            
        var conversationId = Guid.NewGuid().ToString();
        var traceId = activity?.TraceId.ToString();
        
        activity?.SetTag("conversation.id", conversationId);
        
        _currentConversation = new Conversation
        {
            Id = conversationId,
            SystemPrompt = systemPrompt ?? "You are a helpful AI assistant.",
            Messages = new List<Message>(),
            CreatedAt = DateTimeOffset.UtcNow,
            TraceId = traceId,
            Metadata = new Dictionary<string, object>
            {
                ["provider"] = _provider.GetType().Name,
                ["capabilities"] = _provider.GetCapabilities(),
                ["totalInputTokens"] = 0,
                ["totalOutputTokens"] = 0,
                ["totalTokens"] = 0
            }
        };

        _logger.LogInformation("Started new conversation {ConversationId} with trace {TraceId}", 
            conversationId, traceId ?? "none");
            
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Load an existing conversation and continue it.
    /// </summary>
    public async Task<bool> LoadConversationAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _store.LoadAsync(conversationId, cancellationToken);
        
        if (conversation == null)
        {
            _logger.LogWarning("Conversation {ConversationId} not found", conversationId);
            return false;
        }

        _currentConversation = conversation;
        _logger.LogInformation("Loaded conversation {ConversationId} with {MessageCount} messages",
            conversationId, conversation.Messages.Count);
        
        return true;
    }

    /// <summary>
    /// Get the current conversation history.
    /// </summary>
    public List<Message> GetHistory(int? limit = null)
    {
        if (_currentConversation == null)
        {
            return new List<Message>();
        }

        var messages = _currentConversation.Messages;
        
        if (limit.HasValue && limit.Value > 0)
        {
            return messages.TakeLast(limit.Value).ToList();
        }

        return messages.ToList();
    }

    /// <summary>
    /// Get the current conversation ID.
    /// </summary>
    public string? GetCurrentConversationId()
    {
        return _currentConversation?.Id;
    }

    /// <summary>
    /// Get metadata about the current conversation.
    /// </summary>
    public Dictionary<string, object>? GetConversationMetadata()
    {
        return _currentConversation?.Metadata;
    }
}
