# CLAUDE.md

Guidance for Claude Code working on the **TeamFlow Simple Queue** project.

This project is **both a working system and a teaching artifact**. That dual purpose shapes every decision below. When in doubt, optimize for clarity and pedagogical value over cleverness or brevity.

---

## Project Overview

TeamFlow is a research queue backend built in .NET with Semantic Kernel. It accepts research prompts, classifies them as simple or complex, decomposes complex ones into subtasks, performs web research, and produces a final report.

The architecture is a **handoff agent pattern**: three specialist agents (Triage, Research, Synthesizer) pass control to each other through tool calls. The orchestrator enforces budgets, termination, and observability; the agents decide the graph.

The full roadmap lives in `docs/teamflow_roadmap.md`. Read it for context before making design decisions.

---

## How We Work Together

### Build phase-by-phase, never ahead

The roadmap is strictly sequential. Each phase introduces one concept that later phases depend on. If I'm asking about Phase 2, **do not scaffold anything from Phase 3 or beyond**, even if it seems helpful. Stub out future interfaces with `throw new NotImplementedException()` rather than pre-building.

When in doubt about scope, ask: *"Is this needed for the current phase's demo to work?"* If no, it waits.

### Commit at every meaningful step

Small commits with clear messages. After each todo in a phase, commit. This gives us a teachable git history — learners can walk the commits and see the system grow.

### Never delete prior prompt versions

Prompts live in versioned files (`prompts/triage.v1.prompty`, `triage.v2.prompty`, etc.). **Old versions are teaching artifacts. Never delete, never overwrite.** When iterating, create a new version file.

Same rule for bad outputs committed as evidence in Phase 1 — they stay in the repo forever.

### Preserve the pedagogical arc

If a cleaner refactor would obscure the concept being taught, take the less-clean version. Example: in Phase 2, a single `ResearchAgent` is wired directly from code, not through a router. Don't "fix" this by introducing the router early — Phase 4 earns it.

---

## Architectural Rules (Load-Bearing)

These come from the architecture section of the roadmap. Treat as invariants.

### The three agents

- **TriageAgent** — classifies and splits. No external tools. Has handoff tools (after Phase 4) and `split_task` (after Phase 5).
- **ResearchAgent** — performs web investigation. Has MCP-provided tools (search, fetch) plus handoff tools.
- **SynthesizerAgent** — writes the final report. Has a skill loaded into system prompt. **Owns the only terminal action (`finalize`).**

### Handoffs are tool calls, not direct invocations

A handoff is a `[KernelFunction]` that returns a `HandoffRequest` record. The model calls it; the orchestrator intercepts the return value and dispatches. **Agents never call each other directly.** If a proposed change has one agent invoking another, it's wrong — route through `HandoffInterceptor`.

### Intelligence in agents, safety in orchestrator

The orchestrator does **not** decide what runs next. The model does. But the orchestrator owns:

- Budget enforcement (tokens, tool calls, wall time)
- `MAX_HOPS` termination (start at 6)
- Tracing and handoff logging
- Cancellation propagation
- Fresh `ChatHistory` per agent invocation

If I propose putting any of these inside an agent, push back.

### Only Synthesizer can finalize

Triage and Research can hand off to Synth, but cannot end a task themselves. This preserves report quality. No shortcuts.

### Fresh chat history per agent invocation

The `ContextForNext` string on a `HandoffRequest` is the only state that crosses between agents. No shared `ChatHistory`. This is what makes the architecture composable.

### Handoff tools stay in-process (never MCP)

When Phase 7 introduces MCP, only **world-facing tools** (web_search, web_fetch) move to MCP. Handoff tools belong to the orchestrator because they reference in-process state. This distinction matters and is a teaching point.

---

## Phase-Specific Constraints

### Phase 0 — Foundation

No AI calls. No Semantic Kernel usage yet (the packages are installed but unused). The API returns stored prompts verbatim. Resist the urge to stub AI behavior.

### Phase 1 — First Agent

`TriageAgent` only. No tools, no plugins, no handoffs. Just a `ChatCompletionAgent` with instructions and structured output. Keep it small enough that the prompt itself is the whole lesson.

### Phase 2 — First External Tool

Still no handoffs. `TriageAgent` and `ResearchAgent` are connected by C# code directly — "if Simple, call ResearchAgent". This C# glue is **intentionally ugly** because Phase 4 will replace it with handoffs. Don't prematurely elegant-ify.

### Phase 3 — Structured Prompts

This is where prompts move out of C# strings into `.prompty`/resource files. Establish the versioning directory layout here. **Do not collapse old versions into comments.** Separate files, forever.

### Phase 4 — Handoffs (headline phase)

The architecture flips from "C# calls agents in sequence" to "agents hand off via tool calls." When building this:

- Implement `HandoffRequest`, `HandoffPlugin`, `AgentRouter`, `HandoffInterceptor` as distinct types — do not merge them.
- Every handoff function needs a `[Description]` attribute written as a prompt teaching the model *when* to use it.
- The Phase 2 C# glue gets deleted in this phase, not earlier.
- Expose `GET /tasks/{id}/trace` for the handoff log from day one — the trace is the demo.

### Phase 5 — The Queue

Queue uses `System.Threading.Channels` initially. Abstract behind `ITaskQueue` so it can be swapped for Azure Service Bus later without touching the orchestrator. Depth cap at 2 levels — enforce strictly.

### Phase 6 — Skills

Skills live in `/skills/<name>/SKILL.md` with frontmatter. The `SkillLoader` scans at startup. **A new skill must work by adding a folder and restarting — no code change.** If that's not true, the abstraction is broken.

### Phase 7 — MCP

Only external tools move to MCP. Verify parity with existing tests before removing the in-process implementation. The MCP server must be usable from Claude Desktop as proof of portability.

### Phase 8 — Quality

OpenTelemetry spans must nest correctly: task → agent call → tool call. The handoff graph visualization is the headline artifact — treat it as a product feature, not a nice-to-have.

### Phase 9 — Deploy

Eval gate in CI blocks merges on regression. Secrets in Key Vault, never in env vars. One-command teardown must actually work — test it on a fresh clone.

---

## Code Style and Conventions

### File organization

```
/src
  /Api             — ASP.NET Core endpoints, SSE, minimal
  /Core            — Domain types (ResearchTask, HandoffRequest, Finding, Report)
  /Agents          — Agent factories, HandoffPlugin, AgentRouter
  /Orchestration   — HandoffInterceptor, TaskProcessor, QueueWorkerService
  /Infrastructure  — ITaskRepository impls, ITaskQueue impls, MCP clients
  /Tests
/prompts           — versioned .prompty files
/skills            — skill folders (Phase 6+)
/mcp-servers       — standalone MCP server projects (Phase 7+)
/docs              — roadmap, ADRs, prompt style guide
/evals             — eval harness and fixtures (Phase 8+)
```

### Naming

- Agents end in `Agent`: `TriageAgent`, `ResearchAgent`, `SynthesizerAgent`
- Plugins end in `Plugin`: `WebSearchPlugin`, `HandoffPlugin`
- Records for data: `HandoffRequest`, `Finding`, `Report`, `ResearchTask`
- Interfaces start with `I`: `ITaskRepository`, `ITaskQueue`, `ISearchProvider`

### Prompt files

- `prompts/<agent>.v<N>.prompty` — one file per version
- Frontmatter with `name`, `description`, `model`, `temperature`
- Keep all `vN` files in the repo. Current version is referenced by config.

### Tool descriptions are prompts

Every `[KernelFunction]` has a `[Description]`. Every parameter has `[Description]`. Write these as if instructing a careful stranger — because that's what the model is.

### Logging

- Serilog with structured fields
- Every log line in a task's lifecycle includes `taskId` and `parentId`
- Every handoff logs: `sourceAgent`, `targetAgent`, `reasoning`, `hopIndex`
- Every tool call logs: `toolName`, `arguments` (redacted if sensitive), `resultSize`, `durationMs`

---

## Testing Philosophy

### Unit tests

Domain logic (status transitions, depth enforcement, pending-children counter) gets thorough unit tests. These are fast and deterministic.

### Integration tests with fake agents

Agents are expensive and nondeterministic. Use a fake `IChatCompletionService` that returns scripted responses. Tests verify orchestration behavior, not model behavior.

### Snapshot tests for prompts

After Phase 3, snapshot the rendered prompt output for key scenarios. Catches accidental prompt regressions when templates change.

### Evals (Phase 8+)

Real model calls against a fixed prompt set, scored by a judge model. Run nightly and in CI. These are slow and cost money — keep the eval set small and high-signal.

### Do not mock Semantic Kernel itself

Mock the underlying `IChatCompletionService` and `ISearchProvider`. Let SK's own orchestration run in tests.

---

## What to Ask Me About

Please stop and ask before:

- Changing any of the architectural rules above
- Introducing a new agent beyond the three defined
- Moving handoff tools to MCP
- Letting an agent other than Synth finalize a task
- Collapsing prompt versions into a single file
- Adding dependencies beyond what the phase needs

Please **don't** stop and ask for:

- Obvious refactors within a single file
- Adding tests
- Improving log messages
- Fixing compiler warnings

---

## Calibration Notes

The roadmap has a few numbers that are starting guesses, not proven values. Flag them in comments so we remember to tune:

- `MAX_HOPS = 6` — tune after seeing real traces
- Re-triage rate alert at ~5% — may be better as a relative metric
- Tool call budget per task — start generous, tighten based on eval results
- Depth cap of 2 for task decomposition — revisit if complex tasks feel under-served

---

## When Starting a New Phase

1. Read the phase section in `docs/teamflow_roadmap.md`.
2. Create `docs/phases/PHASE_N.md` with the Why/Concept/Demo/Reflection copied in. This becomes the living design doc for that phase.
3. Check off todos in the roadmap as we go (edit the markdown).
4. At the end of the phase, verify the demo actually works end-to-end before moving on.
5. Commit with a phase-boundary message like `Phase 4 complete: handoffs wired, trace endpoint live`.

---

## The North Star

> "Intelligence in the agents; safety in the orchestrator. One primitive (function calling), two uses (world-facing tools, graph-facing handoffs). Prompts are specifications. Evals are the only honest measure."

When a design choice feels unclear, re-read that line. It resolves most of them.