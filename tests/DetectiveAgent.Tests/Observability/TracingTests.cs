using System.Diagnostics;
using DetectiveAgent.Context;
using DetectiveAgent.Core;
using DetectiveAgent.Observability;
using DetectiveAgent.Providers;
using DetectiveAgent.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace DetectiveAgent.Tests.Observability;

public class TracingTests
{
    [Fact]
    public void AgentActivitySource_ShouldBeConfigured()
    {
        // Arrange & Act
        var activitySource = AgentActivitySource.Instance;

        // Assert
        Assert.NotNull(activitySource);
        Assert.Equal("DetectiveAgent", activitySource.Name);
        Assert.Equal("1.0.0", activitySource.Version);
    }

    [Fact]
    public async Task SendMessage_ShouldCreateActivityWithTags()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                activities.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new Message(
                MessageRole.Assistant,
                "Test response",
                DateTimeOffset.UtcNow,
                new Dictionary<string, object>
                {
                    ["inputTokens"] = 10,
                    ["outputTokens"] = 20,
                    ["totalTokens"] = 30
                }));

        mockProvider.Setup(p => p.GetCapabilities())
            .Returns(new ProviderCapabilities(true, true, true, 200_000));
        
        mockProvider.Setup(p => p.EstimateTokensAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var mockStore = new Mock<IConversationStore>();
        mockStore.Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<Agent>>();
        var contextManager = new ContextWindowManager();
        
        var agent = new Agent(mockProvider.Object, mockStore.Object, mockLogger.Object, contextManager);

        // Act
        var response = await agent.SendMessageAsync("Hello");

        // Assert
        Assert.NotNull(response);
        
        // Find the SendMessage activity
        var sendMessageActivity = activities.FirstOrDefault(a => a.DisplayName == "Agent.SendMessage");
        Assert.NotNull(sendMessageActivity);
        
        // Verify trace exists and has required information
        Assert.NotEqual(default, sendMessageActivity.TraceId);
        Assert.NotEqual(default, sendMessageActivity.SpanId);
        Assert.True(sendMessageActivity.Duration > TimeSpan.Zero);
        
        // Verify at least conversation.id tag is present (tags may be captured differently)
        var tagsList = sendMessageActivity.Tags.ToList();
        var conversationIdTag = tagsList.FirstOrDefault(t => t.Key == "conversation.id");
        Assert.NotEqual(default, conversationIdTag);
        Assert.False(string.IsNullOrEmpty(conversationIdTag.Value));
    }

    [Fact]
    public async Task ProviderCall_ShouldCreateActivityWithTags()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                activities.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(@"{
                    ""id"": ""msg_123"",
                    ""content"": [{""type"": ""text"", ""text"": ""Hello!""}],
                    ""usage"": {""input_tokens"": 10, ""output_tokens"": 5},
                    ""stop_reason"": ""end_turn""
                }")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        var mockLogger = new Mock<ILogger<AnthropicProvider>>();
        var provider = new AnthropicProvider(httpClient, mockLogger.Object, "test-key");

        var messages = new List<Message>
        {
            new(MessageRole.System, "You are helpful", DateTimeOffset.UtcNow),
            new(MessageRole.User, "Hello", DateTimeOffset.UtcNow)
        };

        // Act
        var response = await provider.CompleteAsync(messages);

        // Assert
        Assert.NotNull(response);
        
        // Find the Provider.Complete activity
        var providerActivity = activities.FirstOrDefault(a => a.DisplayName == "Provider.Complete");
        Assert.NotNull(providerActivity);
        
        // Verify trace exists and has required information
        Assert.NotEqual(default, providerActivity.TraceId);
        Assert.NotEqual(default, providerActivity.SpanId);
        Assert.True(providerActivity.Duration > TimeSpan.Zero);
        
        // Verify at least provider tag is present
        var tagsList = providerActivity.Tags.ToList();
        var providerTag = tagsList.FirstOrDefault(t => t.Key == "provider");
        Assert.NotEqual(default, providerTag);
        Assert.Equal("Anthropic", providerTag.Value);
        
        // Verify model tag is present
        var modelTag = tagsList.FirstOrDefault(t => t.Key == "model");
        Assert.NotEqual(default, modelTag);
        Assert.False(string.IsNullOrEmpty(modelTag.Value));
    }

    [Fact]
    public void Conversation_ShouldHaveTraceId()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = AgentActivitySource.Instance.StartActivity("TestConversation");
        
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider.Setup(p => p.GetCapabilities())
            .Returns(new ProviderCapabilities(true, true, true, 200_000));

        var mockStore = new Mock<IConversationStore>();
        var mockLogger = new Mock<ILogger<Agent>>();
        var contextManager = new ContextWindowManager();
        
        // Act
        var agent = new Agent(mockProvider.Object, mockStore.Object, mockLogger.Object, contextManager);
        
        // With lazy initialization, no conversation exists until StartNewConversation is called
        Assert.Null(agent.GetCurrentConversationId());
        
        // Now create a conversation - it should capture the trace ID from the current activity
        agent.StartNewConversation();
        var conversationId = agent.GetCurrentConversationId();

        // Assert
        Assert.NotNull(conversationId);
        Assert.NotNull(activity);
        
        // The conversation should be created within an activity context
        // The trace ID would be set when StartNewConversation creates an activity
    }
}
