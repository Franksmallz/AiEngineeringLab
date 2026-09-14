# Chapter 6 Agent Tool-Use Exercise Summary

Built a simple Claude-based agent in C# to understand how agentic systems work in practice.

The application exposed a small set of tools to Claude, including payment status lookup, customer balance lookup, and knowledge-base search. Claude was given the tool definitions and allowed to decide when a tool was required based on the user's request.

I implemented the full tool-use loop:

`User request → Claude → tool_use → application executes tool → tool_result → Claude → final response`

I then extended the loop to support multi-step reasoning, where Claude could call one tool, inspect the result, and decide to call another tool before answering. For example, Claude could retrieve a payment's live status first and then search the internal knowledge base for the appropriate operational guidance.

The knowledge-base tool reused the RAG system built earlier, which helped show the difference between traditional RAG and agentic RAG. In normal RAG, the application decides when retrieval happens. In the agent version, Claude decides whether retrieval is necessary.

I also added failure handling for invalid arguments, missing records, failed knowledge searches, unknown tools, exceptions, and excessive tool calls. Tool execution was protected with application-level controls such as argument validation, permission checks, ownership checks, and tool-call limits.

A major takeaway was that the model should not be treated as the security boundary. Claude can propose an action, but the application must decide whether that action is actually allowed.

This exercise also connected agents back to prompt engineering. Tool descriptions and system prompts strongly influence how Claude selects and uses tools. At the same time, defensive prompt engineering is needed because retrieved knowledge and tool outputs may contain instructions that should not override the agent's system rules.

The exercise helped demystify agents for me. An agent is not a mysterious autonomous system; at its core, it is a model operating inside an application loop where it can choose tools, observe results, and continue reasoning. Prompt engineering guides that behavior, while application-level validation and defensive prompting constrain it.

## What I learned

Agent = model + tools + execution loop + state

Prompt engineering influences:
- which tool gets selected
- when a tool is selected
- how the model interprets tool results
- when the model should stop

Prompt defense influences:
- which instructions are trusted
- how retrieved content is treated
- whether tool output can manipulate the agent

Application code enforces:
- which tools actually exist
- valid arguments
- authorization
- ownership
- execution limits
- safe failure behavior

## Key takeaway

Agents became much less mysterious once I implemented one manually.

The model is not "doing everything."

Claude is mainly deciding:

> "What should I do next?"

The application remains responsible for:

> "What am I actually allowed to do?"
