# Phase 2 — First External Tool

**Concept introduced:** Tools & function calling

## Why

Classification is pure reasoning. Research requires reaching into the world. This phase introduces the single most important pattern in modern agent design: giving the model capabilities and letting it decide when to use them. We introduce it here, on a single agent, with no handoffs yet — so the tool-calling loop is visible in isolation.

## Concept

A tool is a function with a name, a description, and a typed signature. The model does not know how to search the web — it knows how to ask the runtime to search the web. The runtime executes and returns results. The loop continues until the model is done. The description is a prompt; the parameter types are a schema. Both shape behavior.

## What was built

- `WebSearchPlugin` with two `[KernelFunction]` methods:
  - `search(query)` — returns ranked hits from a pluggable provider.
  - `fetch(url)` — returns truncated page text.
  - Each method's `[Description]` is written as a prompt for the model, not a docstring for humans. The description of `search` explicitly tells the model to prefer specific queries and to fetch sparingly.
- `ISearchProvider` interface with `TavilySearchProvider` as the first implementation. Swapping to Brave/Bing later is a new class, not a refactor.
- `ResearchAgent` built via `ResearchAgentFactory`:
  - Auto function calling (`FunctionChoiceBehavior.Auto()`).
  - Structured output enforced with `ResponseFormat = typeof(ResearchResult)`.
  - Fresh kernel + plugin per invocation (no cross-task state — same pattern Phase 4 will rely on).
- `ToolCallBudgetFilter` implements `IAutoFunctionInvocationFilter`, counts calls, and flips `context.Terminate = true` when the cap is reached. The budget is configured via `Research:MaxToolCalls` (default 6 — starting guess; tune after real traces).
- Structured findings: the agent returns a `ResearchResult { answer, findings[] }` where each finding is `{ claim, sourceUrl, confidence }`. Findings are persisted on the task.
- Prompt file `prompts/research.v1.txt` — plain text, same file-per-version layout that Phase 3 will migrate to `.prompty`.
- Wiring in `POST /tasks`: after triage, if `Kind == "Simple"`, the endpoint calls `ResearchAgent` **directly from C#**. This glue is intentionally ugly — Phase 4 deletes it.

### What was *not* built (deferred to later phases)

- Handoffs. `TriageAgent` still has no tools. Research does not call Synth. The C# `if (Simple) ...` check is the orchestration.
- Complex-task decomposition. `Kind == "Complex"` is classified but then ignored by the pipeline — we'll split in Phase 5.
- `.prompty` file format, versioned research prompts beyond v1, prompt style guide. All Phase 3.
- MCP server wrapping `WebSearchPlugin`. Phase 7.

## Demo

```bash
# one-time setup (in src/Api)
dotnet user-secrets set OpenAI:ApiKey "<your openai key>"
dotnet user-secrets set Research:TavilyApiKey "<your tavily key>"

dotnet run --project src/Api

# Simple task — triggers triage + research
curl -X POST http://localhost:5080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"what is the current price of gold"}'
# => { "id":"...", "kind":"Simple",
#      "answer":"Gold is trading around $X/oz as of ...",
#      "findings":[{ "claim":"...", "sourceUrl":"...", "confidence":0.9 }, ...] }

# Complex task — triage classifies, research is skipped until Phase 5
curl -X POST http://localhost:5080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"analyze the EV market in Europe"}'
# => { "id":"...", "kind":"Complex", "answer":null, "findings":null }
```

Watch the logs: every `search` and `fetch` call is logged with tool name, arguments, result size, and duration, plus a terminal line showing total tool calls vs. the configured budget.

### The teaching moment

After the simple task works, change the `[Description]` on `WebSearchPlugin.SearchAsync` from its current wording to something narrower like `"Search only academic papers. Avoid news sites, blogs, and marketing content."` Rerun the same prompt. The model will issue different queries and ignore some URL types — zero code change, just a prompt change. That is the lesson.

## Reflection

> "The tool description is a prompt. You just wrote instructions for a stranger. If the description is vague, the model will use the tool vaguely. Treat every `[KernelFunction]` comment as a public API docstring written for an LLM reader. Remember this — in Phase 4 we'll use the exact same mechanism for something very different."

## Calibration notes

- `Research:MaxToolCalls = 6` is a starting guess. Expect to tune after seeing traces on real tasks.
- `MaxFetchChars = 8000` caps fetched-page size so a single large page cannot blow the context window. May need to grow once we add summarization.
- `Temperature = 0.2` on the research agent — low enough for deterministic tool-choice, high enough to let it paraphrase findings.
- Tavily was chosen for simplicity of integration; swapping to Brave or Bing means writing another `ISearchProvider`, not touching the agent.
- The `if (Simple) call Research` glue is a deliberate anti-pattern. When Phase 4 lands, it gets deleted in one commit — that deletion is part of the lesson.
