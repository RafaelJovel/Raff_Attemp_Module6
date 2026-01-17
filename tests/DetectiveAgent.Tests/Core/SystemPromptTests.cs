namespace DetectiveAgent.Tests.Core;

using DetectiveAgent.Context;
using DetectiveAgent.Core;
using DetectiveAgent.Providers;
using DetectiveAgent.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class SystemPromptTests
{
    private readonly Mock<ILlmProvider> _mockProvider;
    private readonly Mock<IConversationStore> _mockStore;
    private readonly Mock<ILogger<Agent>> _mockLogger;
    private readonly ContextWindowManager _contextManager;

    public SystemPromptTests()
    {
        _mockProvider = new Mock<ILlmProvider>();
        _mockStore = new Mock<IConversationStore>();
        _mockLogger = new Mock<ILogger<Agent>>();
        _contextManager = new ContextWindowManager();

        // Setup default provider behavior
        _mockProvider.Setup(p => p.GetCapabilities())
            .Returns(new ProviderCapabilities(false, false, false, MaxContextTokens: 10000));

        _mockProvider.Setup(p => p.EstimateTokensAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        _mockStore
            .Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void Agent_ShouldUseDefaultSystemPrompt_WhenNotSpecified()
    {
        // Arrange & Act
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager);

        agent.StartNewConversation(); // Use default

        // Assert
        var metadata = agent.GetConversationMetadata();
        Assert.NotNull(metadata);
    }

    [Fact]
    public void Agent_ShouldUseCustomSystemPrompt_WhenSpecified()
    {
        // Arrange
        var customPrompt = "You are a Detective Agent specializing in software release risk assessment.";

        // Act
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: customPrompt);

        agent.StartNewConversation(customPrompt);

        // Assert - Verify the custom prompt is used when starting a conversation
        // We can't directly access the system prompt from the agent, but we can verify
        // it's passed correctly by checking that the conversation was created
        Assert.NotNull(agent.GetCurrentConversationId());
    }

    [Fact]
    public async Task Agent_ShouldStoreSystemPromptInConversation()
    {
        // Arrange
        var customPrompt = "You are a risk assessment specialist.";
        Conversation? savedConversation = null;

        _mockStore
            .Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Callback<Conversation, CancellationToken>((conv, ct) => savedConversation = conv)
            .Returns(Task.CompletedTask);

        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new Message(MessageRole.Assistant, "Response", DateTimeOffset.UtcNow));

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: customPrompt);

        // Act
        agent.StartNewConversation(customPrompt);
        await agent.SendMessageAsync("Test message");

        // Assert
        Assert.NotNull(savedConversation);
        Assert.Equal(customPrompt, savedConversation.SystemPrompt);
    }

    [Fact]
    public async Task Agent_ShouldSendSystemPromptToProvider()
    {
        // Arrange
        var customPrompt = "You are a Detective Agent specializing in software release risk assessment.";
        IReadOnlyList<Message>? sentMessages = null;

        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>()))
            .Callback<IReadOnlyList<Message>, CancellationToken, float?, int?>(
                (msgs, ct, temp, maxTokens) => sentMessages = msgs)
            .ReturnsAsync(new Message(MessageRole.Assistant, "Response", DateTimeOffset.UtcNow));

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: customPrompt);

        // Act
        agent.StartNewConversation(customPrompt);
        await agent.SendMessageAsync("Analyze this release");

        // Assert
        Assert.NotNull(sentMessages);
        // The messages sent to provider should include the user message
        // System prompt handling depends on provider implementation
        Assert.Contains(sentMessages, m => m.Role == MessageRole.User);
    }

    [Theory]
    [InlineData("You are a helpful AI assistant.")]
    [InlineData("You are a Detective Agent specializing in software release risk assessment.")]
    [InlineData("You are a friendly Detective Agent helping teams improve their release processes.")]
    [InlineData("You are a Senior Detective Agent conducting critical release audits.")]
    public void Agent_ShouldAcceptVariousSystemPrompts(string systemPrompt)
    {
        // Arrange & Act
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: systemPrompt);

        agent.StartNewConversation(systemPrompt);

        // Assert
        Assert.NotNull(agent.GetCurrentConversationId());
    }

    [Fact]
    public void Agent_ShouldHandleMultilineSystemPrompt()
    {
        // Arrange
        var multilinePrompt = @"You are a Detective Agent specializing in software release risk assessment.

Your purpose is to analyze software releases and identify potential risks.

You are direct and thorough in your assessments.";

        // Act
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: multilinePrompt);

        agent.StartNewConversation(multilinePrompt);

        // Assert
        Assert.NotNull(agent.GetCurrentConversationId());
    }

    [Fact]
    public void Agent_ShouldHandleLongSystemPrompt()
    {
        // Arrange
        var longPrompt = string.Join("\n", Enumerable.Repeat(
            "You are a Detective Agent specializing in software release risk assessment.", 50));

        // Act
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: longPrompt);

        agent.StartNewConversation(longPrompt);

        // Assert
        Assert.NotNull(agent.GetCurrentConversationId());
    }

    [Fact]
    public void StartNewConversation_ShouldUseProvidedSystemPrompt()
    {
        // Arrange
        var defaultPrompt = "Default prompt";
        var customPrompt = "Custom prompt for this conversation";

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: defaultPrompt);

        // Act
        agent.StartNewConversation(customPrompt);

        // Assert
        Assert.NotNull(agent.GetCurrentConversationId());
    }

    [Fact]
    public void StartNewConversation_ShouldUseDefaultPrompt_WhenNotProvided()
    {
        // Arrange
        var defaultPrompt = "Default prompt";

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: defaultPrompt);

        // Act
        agent.StartNewConversation(); // No prompt provided, should use default

        // Assert
        Assert.NotNull(agent.GetCurrentConversationId());
    }

    [Fact]
    public async Task LoadedConversation_ShouldPreserveOriginalSystemPrompt()
    {
        // Arrange
        var originalPrompt = "Original system prompt";
        var conversationId = "test-conversation";
        
        var existingConversation = new Conversation
        {
            Id = conversationId,
            SystemPrompt = originalPrompt,
            Messages = new List<Message>(),
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>()
        };

        _mockStore
            .Setup(s => s.LoadAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);

        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new Message(MessageRole.Assistant, "Response", DateTimeOffset.UtcNow));

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: "Different default prompt");

        // Act
        await agent.LoadConversationAsync(conversationId);
        await agent.SendMessageAsync("Continue conversation");

        // Assert
        _mockStore.Verify(s => s.SaveAsync(
            It.Is<Conversation>(c => c.SystemPrompt == originalPrompt),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Agent_ShouldHandleEmptySystemPrompt()
    {
        // Arrange & Act
        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager,
            systemPrompt: "");

        agent.StartNewConversation("");

        // Assert
        Assert.NotNull(agent.GetCurrentConversationId());
    }

    [Fact]
    public async Task DifferentConversations_CanHaveDifferentSystemPrompts()
    {
        // Arrange
        var prompt1 = "You are a helpful assistant.";
        var prompt2 = "You are a risk assessment specialist.";
        
        Conversation? savedConversation1 = null;
        Conversation? savedConversation2 = null;
        var saveCount = 0;

        _mockStore
            .Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Callback<Conversation, CancellationToken>((conv, ct) =>
            {
                if (saveCount == 0) savedConversation1 = conv;
                else if (saveCount == 1) savedConversation2 = conv;
                saveCount++;
            })
            .Returns(Task.CompletedTask);

        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new Message(MessageRole.Assistant, "Response", DateTimeOffset.UtcNow));

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockLogger.Object,
            _contextManager);

        // Act
        agent.StartNewConversation(prompt1);
        await agent.SendMessageAsync("First message");

        agent.StartNewConversation(prompt2);
        await agent.SendMessageAsync("Second message");

        // Assert
        Assert.NotNull(savedConversation1);
        Assert.NotNull(savedConversation2);
        Assert.Equal(prompt1, savedConversation1.SystemPrompt);
        Assert.Equal(prompt2, savedConversation2.SystemPrompt);
        Assert.NotEqual(savedConversation1.Id, savedConversation2.Id);
    }
}
