namespace DetectiveAgent.Tests.Tools;

using System.Text.Json;
using DetectiveAgent.Context;
using DetectiveAgent.Core;
using DetectiveAgent.Providers;
using DetectiveAgent.Storage;
using DetectiveAgent.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// Tests for end-to-end tool execution workflow
/// </summary>
public class ToolExecutionTests
{
    private readonly Mock<ILlmProvider> _mockProvider;
    private readonly Mock<IConversationStore> _mockStore;
    private readonly Mock<ILogger<Agent>> _mockAgentLogger;
    private readonly Mock<ILogger<ToolRegistry>> _mockRegistryLogger;
    private readonly Mock<ILogger<ToolExecutor>> _mockExecutorLogger;
    private readonly ContextWindowManager _contextManager;
    private readonly IToolRegistry _toolRegistry;
    private readonly ToolExecutor _toolExecutor;

    public ToolExecutionTests()
    {
        _mockProvider = new Mock<ILlmProvider>();
        _mockStore = new Mock<IConversationStore>();
        _mockAgentLogger = new Mock<ILogger<Agent>>();
        _mockRegistryLogger = new Mock<ILogger<ToolRegistry>>();
        _mockExecutorLogger = new Mock<ILogger<ToolExecutor>>();
        _contextManager = new ContextWindowManager();
        
        // Setup default provider behavior
        _mockProvider.Setup(p => p.GetCapabilities())
            .Returns(new ProviderCapabilities(
                SupportsTools: true,
                SupportsVision: false,
                SupportsStreaming: false,
                MaxContextTokens: 10000));
        
        _mockProvider.Setup(p => p.EstimateTokensAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        _mockStore
            .Setup(s => s.SaveAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Create real tool infrastructure
        _toolRegistry = new ToolRegistry(_mockRegistryLogger.Object);
        _toolExecutor = new ToolExecutor(_toolRegistry, _mockExecutorLogger.Object);
    }

    [Fact]
    public async Task Agent_WithTools_ExecutesToolCallsFromLLM()
    {
        // Arrange
        var toolCallId = "call_123";
        var toolCallExecuted = false;
        
        // Register a test tool
        var testToolDefinition = new ToolDefinition
        {
            Name = "test_tool",
            Description = "A test tool",
            ParametersSchema = JsonDocument.Parse(@"{""type"":""object"",""properties"":{}}"),
            Handler = async (args) =>
            {
                toolCallExecuted = true;
                return new ToolResult
                {
                    ToolCallId = toolCallId,
                    Content = "Tool executed successfully",
                    Success = true,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }
        };
        
        _toolRegistry.RegisterTool(testToolDefinition);

        // Create tool call object
        var toolCall = new ToolCall
        {
            Id = toolCallId,
            Name = "test_tool",
            Arguments = JsonDocument.Parse("{}"),
            Timestamp = DateTimeOffset.UtcNow
        };

        // Mock provider response sequence:
        // 1. First call returns a message with tool calls
        // 2. Second call returns final message without tool calls
        var messageWithToolCall = new Message(
            MessageRole.Assistant,
            "Let me use the tool",
            DateTimeOffset.UtcNow,
            new Dictionary<string, object>
            {
                ["toolCalls"] = new List<ToolCall> { toolCall }
            });

        var finalMessage = new Message(
            MessageRole.Assistant,
            "Tool execution complete!",
            DateTimeOffset.UtcNow);

        _mockProvider
            .SetupSequence(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>(),
                It.IsAny<IReadOnlyList<ToolDefinition>?>()))
            .ReturnsAsync(messageWithToolCall)
            .ReturnsAsync(finalMessage);

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockAgentLogger.Object,
            _contextManager,
            toolRegistry: _toolRegistry,
            toolExecutor: _toolExecutor);

        // Act
        var response = await agent.SendMessageAsync("Use the test tool");

        // Assert
        Assert.True(toolCallExecuted, "Tool should have been executed");
        Assert.Equal("Tool execution complete!", response.Content);
        
        // Verify provider was called twice (once with tool call, once after tool result)
        _mockProvider.Verify(p => p.CompleteAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<float?>(),
            It.IsAny<int?>(),
            It.IsAny<IReadOnlyList<ToolDefinition>?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Agent_WithTools_SendsToolDefinitionsToProvider()
    {
        // Arrange
        IReadOnlyList<ToolDefinition>? sentTools = null;
        
        // Register a test tool
        var testToolDefinition = new ToolDefinition
        {
            Name = "test_tool",
            Description = "A test tool",
            ParametersSchema = JsonDocument.Parse(@"{""type"":""object""}"),
            Handler = async (args) => new ToolResult
            {
                ToolCallId = "test",
                Content = "Success",
                Success = true,
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        
        _toolRegistry.RegisterTool(testToolDefinition);

        // Capture tools sent to provider
        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>(),
                It.IsAny<IReadOnlyList<ToolDefinition>?>()))
            .Callback<IReadOnlyList<Message>, CancellationToken, float?, int?, IReadOnlyList<ToolDefinition>>(
                (msgs, ct, temp, maxTokens, tools) => sentTools = tools)
            .ReturnsAsync(new Message(MessageRole.Assistant, "Response", DateTimeOffset.UtcNow));

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockAgentLogger.Object,
            _contextManager,
            toolRegistry: _toolRegistry,
            toolExecutor: _toolExecutor);

        // Act
        await agent.SendMessageAsync("Test message");

        // Assert
        Assert.NotNull(sentTools);
        Assert.Single(sentTools);
        Assert.Equal("test_tool", sentTools[0].Name);
        Assert.Equal("A test tool", sentTools[0].Description);
    }

    [Fact]
    public async Task Agent_WithMultipleToolCalls_ExecutesAllTools()
    {
        // Arrange
        var tool1Executed = false;
        var tool2Executed = false;
        
        // Register two tools
        var tool1 = new ToolDefinition
        {
            Name = "tool1",
            Description = "First tool",
            ParametersSchema = JsonDocument.Parse(@"{""type"":""object""}"),
            Handler = async (args) =>
            {
                tool1Executed = true;
                return new ToolResult
                {
                    ToolCallId = "call1",
                    Content = "Tool 1 result",
                    Success = true,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }
        };
        
        var tool2 = new ToolDefinition
        {
            Name = "tool2",
            Description = "Second tool",
            ParametersSchema = JsonDocument.Parse(@"{""type"":""object""}"),
            Handler = async (args) =>
            {
                tool2Executed = true;
                return new ToolResult
                {
                    ToolCallId = "call2",
                    Content = "Tool 2 result",
                    Success = true,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }
        };
        
        _toolRegistry.RegisterTool(tool1);
        _toolRegistry.RegisterTool(tool2);

        // Create multiple tool calls
        var toolCalls = new List<ToolCall>
        {
            new ToolCall
            {
                Id = "call1",
                Name = "tool1",
                Arguments = JsonDocument.Parse("{}"),
                Timestamp = DateTimeOffset.UtcNow
            },
            new ToolCall
            {
                Id = "call2",
                Name = "tool2",
                Arguments = JsonDocument.Parse("{}"),
                Timestamp = DateTimeOffset.UtcNow
            }
        };

        var messageWithToolCalls = new Message(
            MessageRole.Assistant,
            "Using both tools",
            DateTimeOffset.UtcNow,
            new Dictionary<string, object>
            {
                ["toolCalls"] = toolCalls
            });

        var finalMessage = new Message(
            MessageRole.Assistant,
            "Both tools executed!",
            DateTimeOffset.UtcNow);

        _mockProvider
            .SetupSequence(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>(),
                It.IsAny<IReadOnlyList<ToolDefinition>?>()))
            .ReturnsAsync(messageWithToolCalls)
            .ReturnsAsync(finalMessage);

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockAgentLogger.Object,
            _contextManager,
            toolRegistry: _toolRegistry,
            toolExecutor: _toolExecutor);

        // Act
        var response = await agent.SendMessageAsync("Use both tools");

        // Assert
        Assert.True(tool1Executed, "Tool 1 should have been executed");
        Assert.True(tool2Executed, "Tool 2 should have been executed");
    }

    [Fact]
    public async Task Agent_WithoutTools_WorksNormally()
    {
        // Arrange - Agent without tools
        _mockProvider
            .Setup(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>(),
                It.IsAny<IReadOnlyList<ToolDefinition>?>()))
            .ReturnsAsync(new Message(MessageRole.Assistant, "Response", DateTimeOffset.UtcNow));

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockAgentLogger.Object,
            _contextManager);
            // No tools registered

        // Act
        var response = await agent.SendMessageAsync("Test message");

        // Assert
        Assert.Equal("Response", response.Content);
        
        // Verify provider was called with null tools
        _mockProvider.Verify(p => p.CompleteAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<float?>(),
            It.IsAny<int?>(),
            null), Times.Once);
    }

    [Fact]
    public async Task Agent_ToolResultsAddedToConversationHistory()
    {
        // Arrange
        var toolCall = new ToolCall
        {
            Id = "call_123",
            Name = "test_tool",
            Arguments = JsonDocument.Parse("{}"),
            Timestamp = DateTimeOffset.UtcNow
        };

        var testTool = new ToolDefinition
        {
            Name = "test_tool",
            Description = "Test",
            ParametersSchema = JsonDocument.Parse(@"{""type"":""object""}"),
            Handler = async (args) => new ToolResult
            {
                ToolCallId = "call_123",
                Content = "Tool result data",
                Success = true,
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        
        _toolRegistry.RegisterTool(testTool);

        var messageWithToolCall = new Message(
            MessageRole.Assistant,
            "Using tool",
            DateTimeOffset.UtcNow,
            new Dictionary<string, object>
            {
                ["toolCalls"] = new List<ToolCall> { toolCall }
            });

        var finalMessage = new Message(
            MessageRole.Assistant,
            "Done",
            DateTimeOffset.UtcNow);

        _mockProvider
            .SetupSequence(p => p.CompleteAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<float?>(),
                It.IsAny<int?>(),
                It.IsAny<IReadOnlyList<ToolDefinition>?>()))
            .ReturnsAsync(messageWithToolCall)
            .ReturnsAsync(finalMessage);

        var agent = new Agent(
            _mockProvider.Object,
            _mockStore.Object,
            _mockAgentLogger.Object,
            _contextManager,
            toolRegistry: _toolRegistry,
            toolExecutor: _toolExecutor);

        // Act
        await agent.SendMessageAsync("Test");
        var history = agent.GetHistory();

        // Assert
        // History should contain: user message, assistant with tool call, tool result, final assistant message
        Assert.True(history.Count >= 3, "History should contain at least user message, tool result, and final response");
        
        // Find the tool result message
        var toolResultMessage = history.FirstOrDefault(m => 
            m.Role == MessageRole.User && 
            m.Content.Contains("Tool result for test_tool"));
        
        Assert.NotNull(toolResultMessage);
        Assert.Contains("Tool result data", toolResultMessage.Content);
    }
}
