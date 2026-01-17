namespace DetectiveAgent.Core;

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

        // Start a new conversation
        StartNewConversation(systemPrompt);
    }

    /// <summary>
    /// Send a message to the agent and get a response.
    /// </summary>
    public async Task<Message> SendMessageAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty", nameof(content));
        }

        if (_currentConversation == null)
        {
            throw new InvalidOperationException("No active conversation");
        }

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

            // Save conversation
            await _store.SaveAsync(_currentConversation, cancellationToken);

            _logger.LogInformation("Received assistant response: {MessagePreview}...",
                assistantMessage.Content.Length > 50 
                    ? assistantMessage.Content.Substring(0, 50) 
                    : assistantMessage.Content);

            return assistantMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during message exchange");
            throw;
        }
    }

    /// <summary>
    /// Start a new conversation, saving the current one if it exists.
    /// </summary>
    public void StartNewConversation(string? systemPrompt = null)
    {
        var conversationId = Guid.NewGuid().ToString();
        
        _currentConversation = new Conversation
        {
            Id = conversationId,
            SystemPrompt = systemPrompt ?? "You are a helpful AI assistant.",
            Messages = new List<Message>(),
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["provider"] = _provider.GetType().Name,
                ["capabilities"] = _provider.GetCapabilities()
            }
        };

        _logger.LogInformation("Started new conversation {ConversationId}", conversationId);
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
