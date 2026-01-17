# Module 6 Project: Detective Agent

**System Context:** 

The Detective Agent will be the first line of defense in a Release Confidence System that helps build confidence in software releases by investigating changes, assessing risks, and producing actionable reports. It will discover what changed in a release and identify potential risks that warrant investigation.

Your task is to build Detective Agent. What follows is the agent's acceptance criteria. The agent can be built using any tools you like. We recommend building it from scratch using your favourite programming and coding assistant, but you can also use a popular SDK, such as LangChain, PydanticAI, and CrewAI, or even a no-code solution such as n8n.

If you're following our recommendation, you'll find instructions for using `DESIGN.md` and `STEPS.md` after the acceptance criteria that will (hopefully) make things easier for you.

## Acceptance Criteria

Some of these ACs can be optional based on how much time/interest you have and how you would like to spend it.

> The optional ACs below are only optional because they don't directly align with our learning objectives for this module; not because they aren't super important in real-world scenarios - they are.

If you know you aren't interested in a specific area, remove it from `STEPS.md` before you create the implementation plan.

### Is conversational

**Acceptance Criteria:**
- User can send messages and receive responses
- Conversation history is maintained in memory during session
- Each conversation is saved as JSON
- Conversation includes all messages with timestamps
- Basic error handling for API failures
- At least 3 automated tests covering core functionality
- Provider abstraction interface is defined and is implemented

### Is transparent

**Observability Acceptance Criteria:**
- Each conversation has a unique trace ID
- Traces saved in structured JSON format
- Spans capture: operation name, duration, start/end times
- Provider call spans include: model, tokens (input/output), duration
- Conversation spans include: message count, total tokens
- Trace files are human-readable and well-organized
- Can correlate conversation JSON with its trace JSON via trace ID
- Automated tests verify trace generation

### Can clear its head (optional)

**Context Window Management Acceptance Criteria:**
- Agent calculates token count before each provider call
- Conversation truncates when within 90% of token limit
- System prompt always preserved
- Most recent N messages preserved
- Context window state visible in traces
- Long conversations don't cause API errors
- Automated tests verify truncation behavior

### Won't give up, right away (optional)

**Retry Logic Acceptance Criteria:**
- Rate limit errors trigger retries
- Retries use exponential backoff
- Max retry attempts configurable
- Jitter added to prevent thundering herd
- Auth/validation errors fail immediately
- Retry attempts tracked in traces with timing
- Automated tests verify retry behavior
- Manual test of rate limit handling

### Has a purpose

**Prompt Engineering Acceptance Criteria:**
- Default system prompt defines agent purpose
- System prompt explains how to behave
- System prompt is easily configurable
- Agent behavior reflects system prompt instructions
- Tested with various prompt configurations

### Can use tools

**Tool Use Acceptance Criteria:**
- Tool abstraction interface defined
- Tool execution loop works end-to-end
- get_release_summary returns mock release data
- file_risk_report accepts and validates risk reports
- Tool calls and results visible in conversation history
- Tool execution captured in traces with timing
- Error handling for tool failures
- Automated tests for tool framework and both tools
- CLI demo of release risk assessment workflow

### Can be evaluated (optional)

**Evaluation Dimensions:**
- 1. Tool Usage Evaluation
- 2. Decision Quality Evaluation
- 3. Error Handling Evaluation
- 4. Regression Tracking
- 5. Structured Report Generation

**Evaluation Acceptance Criteria:**
- Eval framework can run test scenarios automatically
- Tool usage evaluated for correctness and ordering
- Decision quality measured against expected outcomes
- Error handling scenarios validate robustness
- Test suite includes 5+ scenarios covering risk spectrum and error cases
- Regression tracking compares to baseline
- Structured JSON reports generated for automation
- Eval results include pass/fail and diagnostic details
- Automated tests verify eval framework itself
- Documentation explains how to add new eval cases
- CLI supports baseline establishment and comparison


## Building an agent from scratch (with help)

This process has 2 main parts:
1. Create the implementation plan
2. Iteratively follow the steps in the plan

Everything you and your assistant need to know to implement Detective Agent is in `DESIGN.md`. We also recommend a specific order when adding features to your agent. Inside `STEPS.md` you'll find an order of implementation that mirrors the acceptance criteria above and the order we covered these topic during our live session.

We've also provided `PLAN.md` which is an example starter file that a Python developer might use. Feel free to edit this file according to your preferences. Once you're happy with it, you're ready to begin. See `PLAN_after.md` as an example output from step #1 below (and some tweaking throughout the exercise).

## Test Data

You can find some test data in `releases.json`.

## Steps

### 1. Create the implementation plan

The first step is to create the plan you're going to use going forward. Here is an example prompt you can use to create it. Feel free to modify it before using it.

```
Read @PLAN.md and follow your additional instructions inside it. I would like to use OpenRouter as the first provider. For the context window management strategy, I would like to use truncation and only remember the last 6 messages (3 from user + 3 from assistant).
```

Review the plan it created:
- Are the tasks broken down to sufficient detail to avoid overwhelm during implementation?
- Does it follow the same STEPS order?
- Did it add or remove any sections?
- Briefly look over the code - anything unusual?

You may need to collaborate with your assistant to get the planning doc where you're happy with it.


### 2. Iterate over the steps in the plan

This is where you get to decide how much you want to bite off at a time. Hopefully the plan is already broken down well enough. When you're considering the next piece of work, if it doesn't have enough detail, or if it needs to be broken down further, work with your assistant to update PLAN.md until you're happy with what you know it's going to build, or happy enough with the mystery ;)

Here is an example prompt that is a good one to start each step with. Sometimes it may be enough for your assistant to finish it, sometimes it will require some back and forth. Start over if it's too far off at the beginning.

#### Remember to: START EACH STEP WITH A NEW ASSISTANT SESSION

```
Please complete step 1.1 in phase 1 in @PLAN.md. Refer to the appropriate section in @DESIGN.md to ensure you're meeting all of the acceptance criteria.
```

Don't be satisfied with all green on your test run. Test it manually. Make sure you can see what you expect to see in the traces (responses, errors, tool calls, context truncation, retries, etc). Have your assitant create CLI interfaces so you can _verify_ everything is actually working.

#### Remember to: STAGE/COMMIT THE WORKING TREE

Repeat until Detective Agent is ready for duty

> _all of the acceptance criteria are met_

:)
