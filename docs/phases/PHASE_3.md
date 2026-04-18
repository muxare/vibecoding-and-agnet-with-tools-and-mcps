# Phase 3 — Structured Prompts

**Concept introduced:** Prompt engineering — advanced patterns

## Why

With a working agent and real tools, prompt changes now produce visible downstream effects. This phase teaches the named patterns learners will reach for again and again — and crucially, establishes the prompt infrastructure before Phase 4 multiplies the number of prompts we need to maintain.

## Concept

Prompt engineering is not folklore. There are reusable patterns: system/role separation, XML delimiters for structured input, chain-of-thought triggers, output format contracts, and negative examples. We apply each one deliberately and measure the difference.

## What was built

- **`.prompty` file format** — YAML frontmatter (`name`, `version`, `description`, `model`, `temperature`) followed by a template body. `PromptLoader` strips the frontmatter and returns the body.
- **Template variables** — `{{var}}` placeholders are substituted at prompt-load time with values the factory knows: `{{max_tool_calls}}`, `{{max_fetch_chars}}`, `{{current_date}}`. Unknown placeholders pass through unchanged so misses fail loudly rather than silently.
- **`PromptLoader.LoadRendered(dir, agent, version, vars)`** — prefers `.prompty`, falls back to legacy `.txt`. Legacy `.txt` prompts (triage v1–v4, research v1) are untouched — they are the teaching artifact for the earlier phases.
- **Three new research prompt versions, each isolating one pattern:**
  - `research.v2.prompty` — **XML-delimited sections** (`<role>`, `<tools>`, `<constraints>`, `<output_schema>`) + **templated constraints** (budget and date flow in from config).
  - `research.v3.prompty` — v2 plus a **`<process>` chain-of-thought trigger** that walks the model through "restate → decide → justify → re-evaluate" before each tool call.
  - `research.v4.prompty` — v3 plus **negative examples** and an explicit **`<do_not>` list** (no prose, no markdown fences, no fabricated URLs, no compound claims, no duplicate searches).
- **`ResearchAgentFactory` now loads the prompt per-invocation** (not per-DI-singleton) so `{{current_date}}` is fresh on every task. Structured output remains enforced at runtime via `ResponseFormat = typeof(ResearchResult)` — in-prompt schema shapes *what goes in the JSON*; runtime schema keeps the JSON valid.
- **Default research prompt bumped to `v4`** in `appsettings.json`. Swap with a single config flip.
- **Prompt style guide** in `docs/prompts/README.md` — file layout, frontmatter contract, variable catalogue, pattern-by-pattern rationale, and when not to use `.prompty`.

### What was *not* built (deferred to later phases)

- Handoffs. Research still runs as a direct C# call after triage. Phase 4 deletes that glue.
- Triage prompts have not migrated to `.prompty` — `triage.v1–v4.txt` stay as the Phase 1 arc. Migration can happen when triage gains handoff instructions in Phase 4.
- Snapshot tests for rendered prompts. A natural add in Phase 8 (Quality) once evals exist.
- Automatic prompt-version telemetry. Current logs include `PromptVersion` on every research call, which is enough for manual comparison.

## Demo

```bash
dotnet run --project src/Api

# Default is now v4 — run a simple task end to end.
curl -X POST http://localhost:5080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"what is the current price of gold"}'

# Flip to v1 (the pre-structured baseline) and rerun the same task.
#   Edit src/Api/appsettings.json: "Research:PromptVersion" = "v1"
#   Restart the API.
curl -X POST http://localhost:5080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"what is the current price of gold"}'
```

Compare the logs across v1 → v2 → v3 → v4 for the same prompt:

- **Tool calls made** — v3/v4 should call fewer times thanks to the process block.
- **Findings count and shape** — v4 should split compound claims and drop empty-URL findings.
- **Hallucinated URLs** — the `<do_not>` list in v4 suppresses fabricated citations.
- **Tokens used** — later versions use more prompt tokens but fewer completion/tool tokens; the net depends on the task.

Same model, same tools, same input. Only the prompt differs.

### The teaching moment

Diff `research.v1.txt` against `research.v4.prompty`. Every added section maps to a named pattern from the style guide. No single section is clever; the discipline is the whole of it.

## Reflection

> "Structure beats cleverness. A boring prompt with a clear schema outperforms a clever prompt with vague instructions, every time."
>
> "A prompt isn't a wish — it's a spec. The moment you treat it like one (sections, constraints, schemas, anti-patterns), the model starts treating its own output like one too."

## Calibration notes

- `{{current_date}}` is UTC. For time-zone-sensitive queries, add a timezone variable rather than hard-coding one into the prompt.
- The `<examples>` block in v4 is deliberately small. Few-shot scales sub-linearly — two good examples beat ten mediocre ones. Revisit when evals exist (Phase 8).
- The `<process>` block pushes the model toward more thinking tokens between tool calls. On cheap/fast models this can increase latency without improving outcomes; measure on the eval suite before assuming v3 > v2 universally.
- No `.prompty` runtime library is used — we parse frontmatter and substitute variables ourselves. If prompt fanout explodes in Phase 4, consider adopting `Microsoft.SemanticKernel.Prompty` or Handlebars templating rather than growing the hand-rolled parser.
