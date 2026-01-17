namespace DetectiveAgent.Tests.Core;

using DetectiveAgent.Core;
using DetectiveAgent.Providers;
using DetectiveAgent.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class AgentTests
{
    private readonly Mock<ILlmProvider> _mockProvider;
    private readonly Mock<IConversationStore> _mockStore;
    private readonly Mock<ILogger<Agent>> _mockLogger;

    public AgentTests()
    {
        _mockProvider = new Mock<ILlmProvider>();
        _mockStore = new Mock<IConversationStore>();
        _mockLogger = new Mock<ILogger<Agent>>();
    }

    [Fact]
    public async Task SendMessageAsync_ShouldReturnAssistantMessage()
    {
        // Arrange
        var expectedResponse = new Message(
            MessageRole.Assistant,
            "Hello! How can I help you?",
            DateTimeOffset.UtcNow,
            new Dictionary<string, object> { ["model"] = "test-model" });

        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(expectedResponse);

        _mockStore
            .Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object);

        // Act
        var result = await agent.SendMessageAsync("Hi there!");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(MessageRole.Assistant, result.Role);
        Assert.Equal("Hello! How can I help you?", result.Content);
        
        // Verify provider was called
        _mockProvider.Verify(p => p.CompleteAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<float?>(),
            It.IsAny<int?>()), Times.Once);
        
        // Verify conversation was saved
        _mockStore.Verify(s => s.SaveAsync(
            It.IsAny<Conversation>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_ShouldMaintainConversationHistory()
    {
        // Arrange
        var response1 = new Message(MessageRole.Assistant, "Response 1", DateTimeOffset.UtcNow);
        var response2 = new Message(MessageRole.Assistant, "Response 2", DateTimeOffset.UtcNow);

        _mockProvider
            .SetupSequence(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(response1)
            .ReturnsAsync(response2);

        _mockStore
            .Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object);

        // Act
        await agent.SendMessageAsync("Message 1");
        await agent.SendMessageAsync("Message 2");

        var history = agent.GetHistory();

        // Assert
        Assert.Equal(4, history.Count); // 2 user messages + 2 assistant messages
        Assert.Equal(MessageRole.User, history[0].Role);
        Assert.Equal("Message 1", history[0].Content);
        Assert.Equal(MessageRole.Assistant, history[1].Role);
        Assert.Equal(MessageRole.User, history[2].Role);
        Assert.Equal("Message 2", history[2].Content);
        Assert.Equal(MessageRole.Assistant, history[3].Role);
    }

    [Fact]
    public void GetHistory_ShouldReturnEmptyList_WhenNoMessages()
    {
        // Arrange
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object);

        // Clear the initial conversation
        agent.StartNewConversation();

        // Act
        var history = agent.GetHistory();

        // Assert
        Assert.Empty(history);
    }

    [Fact]
    public async Task LoadConversationAsync_ShouldLoadExistingConversation()
    {
        // Arrange
        var conversationId = "test-conversation-id";
        var existingConversation = new Conversation
        {
            Id = conversationId,
            SystemPrompt = "Test prompt",
            Messages = new List<Message>
            {
                new Message(MessageRole.User, "Test message", DateTimeOffset.UtcNow)
            },
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>()
        };

        _mockStore
            .Setup(s => s.LoadAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object);

        // Act
        var loaded = await agent.LoadConversationAsync(conversationId);

        // Assert
        Assert.True(loaded);
        var history = agent.GetHistory();
        Assert.Single(history);
        Assert.Equal("Test message", history[0].Content);
    }

    [Fact]
    public async Task LoadConversationAsync_ShouldReturnFalse_WhenConversationNotFound()
    {
        // Arrange
        var conversationId = "non-existent-id";

        _mockStore
            .Setup(s => s.LoadAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object);

        // Act
        var loaded = await agent.LoadConversationAsync(conversationId);

        // Assert
        Assert.False(loaded);
    }

    [Fact]
    public void StartNewConversation_ShouldCreateNewConversation()
    {
        // Arrange
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object);

        var firstConversationId = agent.GetCurrentConversationId();

        // Act
        agent.StartNewConversation("New system prompt");
        var secondConversationId = agent.GetCurrentConversationId();

        // Assert
        Assert.NotNull(firstConversationId);
        Assert.NotNull(secondConversationId);
        Assert.NotEqual(firstConversationId, secondConversationId);
    }
}
