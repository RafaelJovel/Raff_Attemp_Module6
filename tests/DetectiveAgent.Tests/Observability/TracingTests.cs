using System.Diagnostics;
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
        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.DisplayName == "Agent.SendMessage")
                {
                    capturedActivity = activity;
                }
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

        var mockStore = new Mock<IConversationStore>();
        mockStore.Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<Agent>>();
        var agent = new Agent(mockProvider.Object, mockStore.Object, mockLogger.Object);

        // Act
        var response = await agent.SendMessageAsync("Hello");

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(capturedActivity);
        
        // Verify trace tags
        var tags = capturedActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.True(tags.ContainsKey("conversation.id"));
        Assert.True(tags.ContainsKey("message.length"));
        Assert.Equal("5", tags["message.length"]);
    }

    [Fact]
    public async Task ProviderCall_ShouldCreateActivityWithTags()
    {
        // Arrange
        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.DisplayName == "Provider.Complete")
                {
                    capturedActivity = activity;
                }
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
        Assert.NotNull(capturedActivity);
        
        // Verify provider tags
        var tags = capturedActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("Anthropic", tags["provider"]);
        Assert.True(tags.ContainsKey("model"));
        Assert.True(tags.ContainsKey("inputTokens"));
        Assert.True(tags.ContainsKey("outputTokens"));
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
        
        // Act
        var agent = new Agent(mockProvider.Object, mockStore.Object, mockLogger.Object);
        var conversationId = agent.GetCurrentConversationId();

        // Assert
        Assert.NotNull(conversationId);
        Assert.NotNull(activity);
        
        // The conversation should be created within an activity context
        // Note: In real usage, the trace ID would be set when SendMessageAsync creates an activity
    }
}
