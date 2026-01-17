namespace DetectiveAgent.Storage;

using System.Text.Json;
using DetectiveAgent.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// File system-based conversation storage implementation.
/// </summary>
public class FileSystemConversationStore : IConversationStore
{
    private readonly string _conversationsPath;
    private readonly ILogger<FileSystemConversationStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemConversationStore(
        ILogger<FileSystemConversationStore> logger,
        string? conversationsPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _conversationsPath = conversationsPath ?? Path.Combine("data", "conversations");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Ensure directory exists
        Directory.CreateDirectory(_conversationsPath);
        _logger.LogInformation("Conversation store initialized at {Path}", _conversationsPath);
    }

    public async Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        var filePath = GetConversationFilePath(conversation.Id);
        
        try
        {
            var json = JsonSerializer.Serialize(conversation, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            
            _logger.LogInformation("Saved conversation {ConversationId} to {Path}", 
                conversation.Id, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversation {ConversationId}", conversation.Id);
            throw;
        }
    }

    public async Task<Conversation?> LoadAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var filePath = GetConversationFilePath(conversationId);
        
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Conversation {ConversationId} not found at {Path}", 
                conversationId, filePath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var conversation = JsonSerializer.Deserialize<Conversation>(json, _jsonOptions);
            
            _logger.LogInformation("Loaded conversation {ConversationId} from {Path}", 
                conversationId, filePath);
            
            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public Task<List<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var files = Directory.GetFiles(_conversationsPath, "*.json");
            var conversationIds = files
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .ToList();
            
            _logger.LogInformation("Found {Count} conversations", conversationIds.Count);
            
            return Task.FromResult(conversationIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list conversations");
            throw;
        }
    }

    public Task DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var filePath = GetConversationFilePath(conversationId);
        
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted conversation {ConversationId}", conversationId);
            }
            else
            {
                _logger.LogWarning("Conversation {ConversationId} not found for deletion", conversationId);
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation {ConversationId}", conversationId);
            throw;
        }
    }

    private string GetConversationFilePath(string conversationId)
    {
        return Path.Combine(_conversationsPath, $"{conversationId}.json");
    }
}
