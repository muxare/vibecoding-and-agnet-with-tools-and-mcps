# Phase 1 — First Agent

**Concept introduced:** Prompt engineering — basics

## Why

The simplest useful thing an LLM can do in this system is classify a task. No tools, no handoffs, no orchestration — just prompt in, structured answer out. This is the cleanest possible lesson on how wording shapes output, and it introduces the shape of a Semantic Kernel agent.

## Concept

A prompt is not a question, it is a specification. Learners see how role framing, explicit output schemas, and examples turn unreliable prose into reliable JSON. They also see what a bare `ChatCompletionAgent` looks like — a model, an instructions block, and nothing else.

## What was built

- `TriageAgent` as a bare `ChatCompletionAgent` (no plugins, no tools, no handoffs).
- Four versioned prompt files under `/prompts/`:
  - `triage.v1.txt` — one sentence, no schema, no role
  - `triage.v2.txt` — adds role framing ("You classify research tasks")
  - `triage.v3.txt` — adds an explicit JSON schema in the prompt
  - `triage.v4.txt` — adds two few-shot examples (one simple, one complex)
- Runtime uses SK structured output (`ResponseFormat = typeof(TriageResult)`) so the JSON is enforced at the model boundary, not only by prompt wording.
- Active version is selectable via config (`Triage:PromptVersion`, default `v4`). All versions stay in the repo forever as teaching artifacts.
- `POST /tasks` now classifies synchronously and stores `Kind` ("Simple" | "Complex") on the task.

## Demo

```bash
# one-time setup (in src/Api)
dotnet user-secrets set OpenAI:ApiKey "<your key>"

dotnet run --project src/Api

# simple
curl -X POST http://localhost:5080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"what is the current price of gold"}'
# => { "id":"...", "prompt":"...", "kind":"Simple", ... }

# complex
curl -X POST http://localhost:5080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"analyze the EV market in Europe"}'
# => { "id":"...", "prompt":"...", "kind":"Complex", ... }
```

To show the prompt-engineering arc, switch `Triage:PromptVersion` to `v1`, `v2`, `v3`, `v4` in `appsettings.json` and replay the same two prompts.

### Observed v1 output (teaching evidence)

With `v1` ("Is this simple or complex?"), raw model output is prose like:

> "Determining whether something is 'simple' or 'complex' depends on context. Could you clarify…"

…which trips the `TriageResult` deserializer and fails classification. This is the point of v1: show learners that a one-sentence prompt without a role, schema, or examples is not a specification. Each subsequent version closes one of those gaps.

## Reflection

> "Every prompt version cost us nothing to write, but each one made the system more reliable. Before reaching for fine-tuning or a bigger model, exhaust this lever first."

## Calibration notes

- Model: `gpt-4o-mini` (cheap and fast; supports structured outputs). Revisit if classification quality drops.
- Temperature: `0` for classification — we want stability, not creativity.
- Prompts are plain `.txt` in Phase 1; Phase 3 will migrate these same files to `.prompty` with frontmatter and template variables. File-per-version layout is deliberately established now so Phase 3 is a format swap, not a restructure.
