# Step 5: System Prompt Engineering

## Overview

System prompts are the foundation of agent behavior. They define the agent's purpose, capabilities, personality, and boundaries. This document explains the Detective Agent's prompt engineering approach and provides examples for different use cases.

## Current Implementation

The Detective Agent's system prompt is:
- **Configurable**: Defined in `appsettings.json` under `Agent:SystemPrompt`
- **Environment-specific**: Can be overridden in `appsettings.Development.json` or other environment configs
- **Injected at runtime**: Loaded via `IConfiguration` and passed to the Agent constructor
- **Stored with conversations**: Each conversation preserves its system prompt for consistency

## Default Detective Agent Prompt

The production prompt (`appsettings.json`) defines the agent as:

```
You are a Detective Agent specializing in software release risk assessment.

Your purpose is to analyze software releases and identify potential risks that could 
impact deployment success...
```

### Key Elements:
1. **Identity**: "Detective Agent specializing in software release risk assessment"
2. **Capabilities**: Lists what the agent excels at
3. **Process**: Step-by-step approach to analysis
4. **Personality**: "Direct and thorough", "don't sugarcoat"
5. **Boundaries**: "stay focused on... software release assessment"
6. **Responsibility**: "Your analysis directly impacts deployment decisions"

## Prompt Engineering Principles

### 1. Clear Identity
Define WHO the agent is:
- Role/profession
- Area of specialization
- Core purpose

**Example:**
> "You are a Detective Agent specializing in software release risk assessment."

### 2. Explicit Capabilities
State WHAT the agent can do:
- Key skills
- Areas of expertise
- Tools or methods available

**Example:**
> "You excel at:
> - Examining release summaries, test results, and deployment metrics
> - Identifying patterns that indicate potential problems"

### 3. Process Guidance
Describe HOW the agent should work:
- Step-by-step approach
- Decision-making criteria
- Expected outputs

**Example:**
> "When analyzing a release, you should:
> 1. Gather comprehensive information about the changes
> 2. Evaluate test coverage and failure patterns
> 3. Assess the overall risk level: HIGH, MEDIUM, or LOW"

### 4. Personality & Tone
Define HOW the agent communicates:
- Communication style
- Tone (formal, friendly, direct)
- Values or principles

**Example:**
> "You are direct and thorough in your assessments. You don't sugarcoat risks, 
> but you also don't exaggerate them."

### 5. Boundary Setting
Clarify what the agent SHOULD NOT do:
- Out-of-scope topics
- Behavior to avoid
- Limitations to acknowledge

**Example:**
> "When you encounter missing or incomplete information, you ask specific questions 
> rather than making assumptions. You stay focused on the task at hand and don't 
> engage in topics outside of software release assessment."

### 6. Responsibility & Impact
Connect the agent's work to REAL-WORLD CONSEQUENCES:
- Why accuracy matters
- Who relies on the output
- Impact of errors

**Example:**
> "Remember: Your analysis directly impacts deployment decisions. Be accurate, 
> thorough, and clear."

## Example Prompt Variations

### Variation 1: Collaborative Development Mode
**Use Case**: Development/testing environment where teams want supportive feedback

**Configuration**: `appsettings.Development.json`

```json
{
  "Agent": {
    "SystemPrompt": "You are a friendly Detective Agent specializing in software release risk assessment.\n\nYour purpose is to help teams understand potential risks in their software releases in a collaborative and supportive way.\n\nWhen analyzing releases:\n- Take a thorough but approachable tone\n- Explain technical concepts clearly\n- Highlight both risks AND positive aspects\n- Encourage questions and discussion\n- Provide guidance on how to mitigate identified risks\n\nYou balance being thorough with being encouraging. You help teams improve their release processes while maintaining confidence in their work.",
    "Temperature": 0.5
  }
}
```

**Key Differences from Production:**
- Friendlier tone: "collaborative and supportive"
- Balanced feedback: "both risks AND positive aspects"
- Educational focus: "Explain technical concepts clearly"
- Lower temperature (0.5) for more consistent, focused responses

### Variation 2: Strict Audit Mode
**Use Case**: High-stakes production releases requiring maximum scrutiny

**Configuration**: `appsettings.Audit.json`

```json
{
  "Agent": {
    "SystemPrompt": "You are a Senior Detective Agent conducting critical release audits.\n\nYour mandate is to identify ALL potential risks in software releases before production deployment. You operate with zero tolerance for ambiguity.\n\nYour audit process:\n1. Systematically examine every data point provided\n2. Flag ANY concerning signals, regardless of severity\n3. Require complete information - reject incomplete submissions\n4. Classify risks conservatively (when in doubt, escalate severity)\n5. Document precise evidence for every finding\n\nYou are uncompromising in your thoroughness. Missing data is treated as HIGH RISK until proven otherwise. Incomplete test coverage triggers mandatory review.\n\nYour findings determine go/no-go decisions for production deployment. There are no second chances. Be exhaustive.",
    "Temperature": 0.3,
    "MaxTokens": 4096
  }
}
```

**Key Differences:**
- Stricter identity: "Senior Detective Agent conducting critical release audits"
- Zero ambiguity tolerance
- Conservative risk classification
- Higher stakes: "go/no-go decisions"
- Lower temperature (0.3) for more deterministic responses
- Higher max tokens for detailed findings

### Variation 3: Research & Analysis Mode
**Use Case**: Exploratory analysis, pattern discovery, trend identification

**Configuration**: `appsettings.Research.json`

```json
{
  "Agent": {
    "SystemPrompt": "You are an Analytical Detective Agent specializing in software release pattern analysis.\n\nYour purpose is to discover insights, patterns, and trends across releases. You approach each release as a learning opportunity.\n\nYour analytical approach:\n- Look beyond surface-level data for deeper patterns\n- Compare current release against historical trends\n- Identify correlations between metrics\n- Hypothesize about root causes of anomalies\n- Suggest areas for further investigation\n\nYou are curious and exploratory. You ask \"why\" questions. You connect dots that others might miss. You provide context and historical perspective.\n\nYour goal is insight generation, not just risk identification. Help teams understand the story behind the data.",
    "Temperature": 0.8,
    "MaxTokens": 3072
  }
}
```

**Key Differences:**
- Focus on patterns and insights vs. binary risk assessment
- Exploratory tone: "curious and exploratory"
- Historical context: "Compare current release against historical trends"
- Higher temperature (0.8) for more creative connections

### Variation 4: Minimal/Generic Assistant
**Use Case**: Testing, development, or general purpose conversation

**Configuration**: Custom or test scenarios

```json
{
  "Agent": {
    "SystemPrompt": "You are a helpful AI assistant.",
    "Temperature": 0.7,
    "MaxTokens": 2048
  }
}
```

**Use Cases:**
- Unit testing where prompt content doesn't matter
- Generic conversation testing
- Baseline comparison for prompt effectiveness

## Configuration Best Practices

### 1. Environment-Specific Prompts

Use .NET configuration hierarchy:
- `appsettings.json` - Production defaults
- `appsettings.Development.json` - Development overrides
- `appsettings.Staging.json` - Staging environment
- `appsettings.Production.json` - Production overrides
- Environment variables - Runtime overrides

**Example:**
```bash
# Override system prompt via environment variable
export Agent__SystemPrompt="Custom prompt for this deployment"
dotnet run --project samples/DetectiveAgent.Cli
```

### 2. Temperature Tuning

Match temperature to prompt purpose:
- **0.0-0.3**: Deterministic, factual, consistent (audits, compliance)
- **0.3-0.5**: Balanced, focused, professional (standard operations)
- **0.5-0.7**: Creative, conversational, helpful (development, support)
- **0.7-1.0**: Exploratory, diverse, insightful (research, brainstorming)

### 3. Token Budget Allocation

Adjust `MaxTokens` based on expected output:
- **512-1024**: Short, focused responses
- **1024-2048**: Standard conversation, typical analysis
- **2048-4096**: Detailed analysis, comprehensive reports
- **4096+**: Extended research, multi-faceted investigations

### 4. Prompt Testing

Always test prompt changes:
1. Write test scenarios that exercise the prompt
2. Validate agent behavior matches expectations
3. Compare responses across prompt variations
4. Document behavioral differences

## Testing Prompts

### Manual Testing

```bash
# Test production prompt
dotnet run --project samples/DetectiveAgent.Cli

# Test development prompt
ASPNETCORE_ENVIRONMENT=Development dotnet run --project samples/DetectiveAgent.Cli

# Test custom prompt
export Agent__SystemPrompt="Your custom prompt here"
dotnet run --project samples/DetectiveAgent.Cli
```

### Automated Testing

See `tests/DetectiveAgent.Tests/Core/SystemPromptTests.cs` for examples of:
- Testing agent behavior with different prompts
- Verifying prompt is properly stored in conversations
- Validating prompt affects response characteristics

## Prompt Evolution

As the Detective Agent adds new capabilities (especially in Step 6: Tool Abstraction), the system prompt will evolve to include:

### Tool Usage Instructions
```
You have access to tools that extend your capabilities:
- get_release_summary: Retrieves detailed information about a release
- file_risk_report: Submits your risk assessment findings

When you need information, use get_release_summary. When you've completed your 
analysis, use file_risk_report to submit your findings.
```

### Response Format Guidance
```
When filing a risk report, structure your findings as:
- Severity: HIGH, MEDIUM, or LOW
- Key Findings: Bulleted list of specific risks identified
- Reasoning: Clear explanation of your risk assessment
```

### Multi-Agent Coordination
```
You are part of a team of agents. Your role is initial risk detection. When you 
identify complex issues requiring specialized expertise, you may hand off to 
specialized agents.
```

## Acceptance Criteria Verification

Step 5 Acceptance Criteria:
- ✅ Default system prompt defines agent purpose (Detective Agent for release risk assessment)
- ✅ System prompt explains how to behave (direct, thorough, focused process)
- ✅ System prompt is easily configurable in appsettings.json
- ✅ Agent behavior reflects system prompt instructions (to be verified in testing)
- ✅ Tested with various prompt configurations (see SystemPromptTests.cs)
- ✅ System prompt stored in configuration and injected via constructor

## Future Enhancements

### 1. Prompt Templates
Create a library of reusable prompt components:
```csharp
public static class PromptTemplates
{
    public const string RiskAssessmentIdentity = 
        "You are a Detective Agent specializing in software release risk assessment.";
    
    public const string ToolUsageInstructions = 
        "You have access to tools...";
    
    public static string Build(params string[] components) 
        => string.Join("\n\n", components);
}
```

### 2. Dynamic Prompt Generation
Generate prompts based on:
- Available tools
- User preferences
- Deployment environment
- Historical context

### 3. Prompt Versioning
Track prompt changes over time:
- Version numbers in conversation metadata
- A/B testing different prompts
- Performance metrics by prompt version

### 4. Few-Shot Examples
Add example interactions to guide behavior:
```
Here's an example of good risk assessment:

User: Analyze release v2.1.0
You: Let me get the release details first...
[Uses get_release_summary tool]
You: Based on the release summary, I've identified...
```

## References

- [Anthropic Prompt Engineering Guide](https://docs.anthropic.com/claude/docs/prompt-engineering)
- [OpenAI Prompt Engineering](https://platform.openai.com/docs/guides/prompt-engineering)
- Agent Configuration: `src/DetectiveAgent/Core/Agent.cs`
- Configuration Loading: `samples/DetectiveAgent.Cli/Program.cs`
- Tests: `tests/DetectiveAgent.Tests/Core/SystemPromptTests.cs`
