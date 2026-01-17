namespace DetectiveAgent.Storage;

using DetectiveAgent.Core;

/// <summary>
/// Interface for persisting and retrieving conversations.
/// </summary>
public interface IConversationStore
{
    /// <summary>
    /// Save a conversation to persistent storage.
    /// </summary>
    Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a conversation from persistent storage.
    /// </summary>
    Task<Conversation?> LoadAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all stored conversation IDs.
    /// </summary>
    Task<List<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a conversation from persistent storage.
    /// </summary>
    Task DeleteAsync(string conversationId, CancellationToken cancellationToken = default);
}
