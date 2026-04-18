# TeamFlow Simple Queue — Pedagogical Build Roadmap

**Revision 2 — Handoff Agent Architecture**

.NET · Semantic Kernel · Tools · MCP · Skills · Prompt Engineering

---

## Project Summary

TeamFlow is a research queue backend built in .NET with Semantic Kernel. It accepts research prompts, classifies them as simple or complex, decomposes complex ones into subtasks, performs web research, and produces a final report. The interesting part is the architecture: three specialist agents that pass control to each other via handoff tool calls, orchestrated by a queue-backed worker.

This project is both a working system and a teaching artifact. Every phase introduces one concept at its simplest form, then layers the next concept on top.

---

## Learning Goals

The four concepts we teach:

- **Tools & function calling** — giving an LLM structured capabilities it can choose to invoke. In this project, tools do two distinct jobs: act on the world (web search, fetch) and act on the graph (handoffs between agents).
- **MCP (Model Context Protocol)** — exposing tools as a reusable server other agents can consume.
- **Skills** — packaging domain know-how as a loadable bundle of prompts, examples, and helpers.
- **Prompt engineering** — designing the instructions, structure, and examples that shape agent behavior. With handoffs, prompts now also specify state machine transitions, not just behavior.

Teaching pattern per phase:

- **Why** — the problem this phase solves.
- **Concept** — the one idea being introduced.
- **Todos** — concrete tasks that implement it.
- **Demo** — what you can show after completing the phase.
- **Reflection** — what to discuss with learners before moving on.

---

## The Agent Architecture

TeamFlow uses a handoff agent pattern. Three specialist agents pass control to each other through a single primitive: a handoff is a tool call. The model decides where to go next; the orchestrator enforces the graph, the budget, and the termination condition.

### The three agents

**TriageAgent** — classifies a task and optionally splits it into subtasks. Has handoff tools and a split tool. No external tools. Runs on a cheap, fast model.

**ResearchAgent** — performs web investigation. Has MCP-provided tools (search, fetch) plus handoff tools. Runs on the best reasoning model because quality of findings gates everything downstream.

**SynthesizerAgent** — writes the final report. Has a skill loaded into its system prompt. Owns the only terminal action (`finalize`). No external tools. Runs on a strong writing model.

### Handoffs as tool calls

Each agent has a set of handoff functions exposed as `[KernelFunction]`s. The model calls one when it decides another agent should take over. The function does not actually invoke the next agent — it returns a `HandoffRequest`. The orchestrator intercepts that return value, logs the transition, and dispatches to the target agent. This keeps the orchestrator in control of budgets, tracing, and cancellation.

### Why this fits the pedagogical goal

In a simpler architecture, orchestration and tool use are separate concepts introduced in different phases. Here they are unified: once learners understand function calling in Phase 2, orchestration in Phase 4 is the same primitive pointed inward. "Tools act on the world; handoffs act on the graph." One mechanism, two uses.

### The topology

```
                    ┌─────────────────────────────────┐
                    │   QueueWorker dequeues a task   │
                    │   Starts it in TriageAgent      │
                    └────────────────┬────────────────┘
                                     │
                                     ▼
                            ┌──────────────────┐
                            │   TriageAgent    │
                            │  split_task      │
                            │  handoff→research│
                            │  handoff→synth   │
                            └────┬─────────┬───┘
                                 │         │
                  ┌──────────────┘         └───────────┐
                  ▼                                    ▼
         ┌──────────────────┐               ┌──────────────────┐
         │  ResearchAgent   │◀─re-triage?──│  (rarely)        │
         │  web_search (MCP)│               │  direct to synth │
         │  web_fetch  (MCP)│               │                  │
         │  handoff→synth   │               └──────────────────┘
         │  handoff→triage  │
         └────────┬─────────┘
                  │
                  ▼
         ┌──────────────────┐
         │ SynthesizerAgent │
         │  finalize (end)  │
         │  + loaded skill  │
         └──────────────────┘
```

### The handoff primitive

```csharp
public record HandoffRequest(
    string TargetAgent,     // "research" | "synth" | "triage"
    string Reasoning,       // why the model chose this handoff
    string ContextForNext   // what the next agent needs to know
);

[KernelFunction, Description(
    "Hand off to research when the task needs web investigation. " +
    "Provide reasoning and the specific research question.")]
public HandoffRequest HandoffToResearch(string reasoning, string question)
    => new("research", reasoning, question);
```

### The orchestrator loop (pseudo-code)

```
async RunTask(task):
  currentAgent = triageAgent
  context = task.Prompt
  hops = 0

  while hops < MAX_HOPS:
    result = await currentAgent.InvokeAsync(context)

    if result is HandoffRequest:
      LogTransition(from, to, reasoning)
      currentAgent = Resolve(result.TargetAgent)
      context = result.ContextForNext
      hops++
      continue

    if result is SplitRequest:
      foreach sub in result.Subtasks:
        queue.Enqueue(CreateChild(task, sub))
      task.Status = Splitting; return

    if result is FinalizeRequest:
      task.Report = result.Report
      task.Status = Done
      NotifyParent(task.ParentId); return

  throw MaxHopsExceeded(task.Id)
```

### Rules of the road

- **Only the synthesizer can finalize.** Triage and research can hand off to synth but cannot end the task themselves. This preserves quality — no shortcuts around the report.
- **Fresh chat history per agent invocation.** The `ContextForNext` string is the only carryover. This forces each handoff to articulate what matters; the articulation is the handoff's value.
- **MAX_HOPS is non-negotiable.** Start at 6. Without it, two agents can ping-pong forever.
- **Budgets live in the orchestrator.** Token count, tool-call count, wall time — all enforced around `InvokeAsync`, not inside the agents themselves.
- **Log every handoff.** Source agent, target agent, reasoning, and context become the most interesting demo artifact the project produces.

---

## Roadmap at a Glance

| Phase | Focus                | New concept                   | What learners see                                                     |
| ----- | -------------------- | ----------------------------- | --------------------------------------------------------------------- |
| 0     | Foundation           | Project shape                 | A running API that echoes prompts back                                |
| 1     | First agent          | Prompt engineering (basics)   | TriageAgent classifies simple vs complex                              |
| 2     | First external tool  | Tools & function calling      | ResearchAgent chooses to call web search                              |
| 3     | Structured prompts   | Prompt engineering (advanced) | Same agent, three prompt versions, visibly different results          |
| 4     | Handoffs             | Tools pointed inward          | Agents pass control to each other; orchestrator logs every transition |
| 5     | The queue            | Decomposition & synthesis     | Complex task splits, children run, parent rolls up                    |
| 6     | Skills               | Portable expertise            | Swapping a skill changes report style with no code change             |
| 7     | MCP                  | Model Context Protocol        | Research tools run as an external MCP server                          |
| 8     | Quality              | Evals & observability         | A dashboard of cost, latency, eval scores, and handoff graphs         |
| 9     | Deploy               | Ship it                       | Public URL, CI/CD with eval gates, one-command teardown               |

---

## Phase 0 — Foundation

**Concept introduced:** Project shape (no AI yet)

### Why

Before we wire up an LLM, we need somewhere for it to live. This phase gives learners a bare-bones API and a mental model of the codebase so later AI concepts drop into familiar slots.

### Todos

- [ ] Scaffold ASP.NET Core minimal API (net9.0)
- [ ] Solution layout: `Api`, `Core` (domain), `Agents`, `Infrastructure`, `Tests`
- [ ] Add Semantic Kernel packages but do not use them yet
- [ ] `POST /tasks` that stores a prompt and returns an ID (no AI)
- [ ] `GET /tasks/{id}` that returns the stored prompt
- [ ] In-memory repository behind an `ITaskRepository` interface
- [ ] Serilog with structured logging and a `taskId` correlation field
- [ ] User-secrets wired for API keys you will need soon

### Demo

Send a POST, get an ID, GET it back. Boring — and that is the point.

### Reflection

> "Notice there is no intelligence here yet. Everything that follows is added at specific, named places in this skeleton. The shape of the app does not change — only what happens inside one method."

---

## Phase 1 — First Agent

**Concept introduced:** Prompt engineering — basics

### Why

The simplest useful thing an LLM can do in this system is classify a task. No tools, no handoffs, no orchestration — just prompt in, structured answer out. This is the cleanest possible lesson on how wording shapes output, and it introduces the shape of a Semantic Kernel agent.

### Concept

A prompt is not a question, it is a specification. Learners see how role framing, explicit output schemas, and examples turn unreliable prose into reliable JSON. They also see what a bare `ChatCompletionAgent` looks like — a model, an instructions block, and nothing else.

### Todos

- [x] Build `TriageAgent` as a `ChatCompletionAgent` with no plugins
- [x] Write v1 prompt: a single sentence — "Is this simple or complex?"
- [x] Observe messy output; commit it as teaching evidence
- [x] Iterate to v2: add role ("You classify research tasks")
- [x] Iterate to v3: add explicit JSON schema in the prompt
- [x] Iterate to v4: add two few-shot examples (one simple, one complex)
- [x] Use SK structured output to enforce the JSON schema at runtime
- [x] Store each prompt version as a file so learners can diff them
- [x] Wire `TriageAgent` into `POST /tasks`; store `Kind` on the task

### Demo

Submit "what is the current price of gold" (simple) and "analyze the EV market in Europe" (complex). Show the v1 → v4 evolution side by side — same model, same input, dramatically different reliability.

### Reflection

> "Every prompt version cost us nothing to write, but each one made the system more reliable. Before reaching for fine-tuning or a bigger model, exhaust this lever first."

---

## Phase 2 — First External Tool

**Concept introduced:** Tools & function calling

### Why

Classification is pure reasoning. Research requires reaching into the world. This phase introduces the single most important pattern in modern agent design: giving the model capabilities and letting it decide when to use them. We introduce it here, on a single agent, with no handoffs yet — so the tool-calling loop is visible in isolation.

### Concept

A tool is a function with a name, a description, and a typed signature. The model does not know how to search the web — it knows how to ask the runtime to search the web. The runtime executes and returns results. The loop continues until the model is done. The description is a prompt; the parameter types are a schema. Both shape behavior.

### Todos

- [x] Create `WebSearchPlugin` with two `[KernelFunction]` methods: `search(query)` and `fetch(url)`
- [x] Write crisp XML-doc descriptions on each — these are the prompts the model reads
- [x] Pick one search provider behind an `ISearchProvider` interface (Brave, Tavily, or Bing)
- [x] Build `ResearchAgent` with the plugin and auto function calling enabled
- [x] Log every tool call with arguments and result size
- [x] For now: wire `TriageAgent` to return `Kind`; if Simple, call `ResearchAgent` directly from code
- [x] Add a tool-call budget (max *N* calls per task) enforced in the invocation loop
- [x] Return findings as structured records: `{ claim, sourceUrl, confidence }`

### Demo

Submit "what is the current price of gold". Show the trace: model calls `search`, reads results, calls `fetch` on one URL, produces the answer with citation. Then change the tool description from "Search the web" to "Search only academic papers" and rerun — watch behavior change with zero code change.

### Reflection

> "The tool description is a prompt. You just wrote instructions for a stranger. If the description is vague, the model will use the tool vaguely. Treat every `[KernelFunction]` comment as a public API docstring written for an LLM reader. Remember this — in Phase 4 we'll use the exact same mechanism for something very different."

---

## Phase 3 — Structured Prompts

**Concept introduced:** Prompt engineering — advanced patterns

### Why

With a working agent and real tools, prompt changes now produce visible downstream effects. This phase teaches the named patterns learners will reach for again and again — and crucially, establishes the prompt infrastructure before Phase 4 multiplies the number of prompts we need to maintain.

### Concept

Prompt engineering is not folklore. There are reusable patterns: system/role separation, XML delimiters for structured input, chain-of-thought triggers, output format contracts, and negative examples. We apply each one deliberately and measure the difference.

### Todos

- [x] Move prompts out of C# strings into `.prompty` or resource files
- [x] Template variables with Handlebars or SK prompt templates
- [x] Version prompts (v1, v2, ...) and keep old versions alongside
- [x] Introduce XML-delimited input sections: `<task>`, `<constraints>`, `<examples>`
- [x] Add a "think step by step" block to `ResearchAgent` and measure quality change
- [x] Enforce output schemas with SK's structured output or JSON mode
- [x] Add negative examples — "do NOT return prose, do NOT include URLs without citation"
- [x] Write a prompt style guide in `docs/prompts/README.md`

### Demo

Run the same task through v1 and v4 prompts of `ResearchAgent`. Show a table of: tokens used, tool calls made, citations returned, hallucinations caught. Same model, same tools — prompt alone moves every metric.

### Reflection

> "Structure beats cleverness. A boring prompt with a clear schema outperforms a clever prompt with vague instructions, every time."

---

## Phase 4 — Handoffs: Tools Pointed Inward

**Concept introduced:** Agent-driven orchestration via function calls

### Why

We have two agents that each do one job well. In most architectures we would now write a C# orchestrator that runs them in sequence. Instead, we give the agents handoff tools and let them decide the sequence themselves. This is the headline phase of the course — the moment where tools stop being about the outside world and start being about control flow.

### Concept

A handoff is a `[KernelFunction]` that returns a `HandoffRequest`. The model calls it; the orchestrator intercepts the return value, logs the transition, and dispatches to the target agent. The orchestrator never makes routing decisions — the agents do. But the orchestrator still owns budgets, tracing, termination, and `MAX_HOPS` enforcement. Intelligence is in the agents; safety is in the orchestrator.

### Todos

- [ ] Define `HandoffRequest` record: `TargetAgent`, `Reasoning`, `ContextForNext`
- [ ] Build `HandoffPlugin` with `HandoffToResearch`, `HandoffToSynth`, `HandoffBackToTriage`
- [ ] Write descriptions on each handoff function — these teach the model when to use them
- [ ] Build a stub `SynthesizerAgent` with one tool: `finalize(report)` — terminal action
- [ ] Add `HandoffPlugin` to `TriageAgent` and `ResearchAgent` kernels
- [ ] Restrict `finalize` to `SynthesizerAgent` only — no shortcuts around the report
- [ ] Build `AgentRouter`: resolves agent name → `ChatCompletionAgent` instance
- [ ] Build `HandoffInterceptor`: runs the `InvokeAsync` loop, catches `HandoffRequest` returns
- [ ] Enforce `MAX_HOPS` (start at 6); throw `MaxHopsExceeded` on violation
- [ ] Fresh `ChatHistory` per agent invocation — only `ContextForNext` carries between
- [ ] Log every handoff: source, target, reasoning, timestamp, hop index
- [ ] Expose the handoff log via `GET /tasks/{id}/trace`
- [ ] Update prompts: each agent's system prompt now explains when to hand off where

### Demo

Submit a simple task. Watch the trace: Triage hands off to Research with reasoning, Research calls `web_search`, then hands off to Synth with findings, Synth calls `finalize`. Now edit `ResearchAgent`'s prompt to be more cautious. Submit the same task. Watch Research hand off back to Triage mid-task because it decided the scope was wrong. Same code, different prompt, different graph.

### Reflection

> "In Phase 2 we used function calling to give the model capabilities in the world. Here we used the exact same mechanism to give it capabilities on the graph. One primitive, two jobs. That generality is why function calling is the foundational pattern of modern agent systems."

> "Notice what the orchestrator does NOT do: it does not decide what runs next. That decision is in the model. But the orchestrator still owns every guarantee we care about — budget, termination, observability. Keep intelligence in the agents; keep safety in the orchestrator."

---

## Phase 5 — The Queue: Decomposition & Synthesis

**Concept introduced:** Recursive task breakdown

### Why

So far we have handled simple tasks end-to-end. Complex tasks need decomposition: Triage splits them into children, each child runs through the full agent graph, and the parent synthesizes their reports. This phase wires the queue and turns a chat wrapper into TeamFlow.

### Concept

Split and finalize are structural tools that change task state rather than just routing control flow. When Triage calls `split_task`, the parent pauses and children enter the queue. When the last child finishes, the parent wakes up and is dispatched to Synth. Recursion handled by a counter, not by the model.

### Todos

- [ ] Implement `ITaskQueue` over `System.Threading.Channels`
- [ ] `QueueWorkerService` as a `BackgroundService` consuming the channel
- [ ] Add `SplitTaskRequest` to `TriageAgent`'s toolbox (returns list of subtasks)
- [ ] Orchestrator: on `SplitRequest`, enqueue children and return — parent waits
- [ ] Child/parent relationships with pending-children counter in the repository
- [ ] `NotifyParent` decrements counter; on zero, re-enqueue parent targeted at Synth
- [ ] `SynthesizerAgent` gets real prompt: compose report from children's reports
- [ ] Depth cap (max 2 levels) — refuse further splits past cap, treat as simple
- [ ] Partial-failure policy: synthesize with available child findings + note what failed
- [ ] SSE endpoint `GET /tasks/{id}/events` streaming status and handoff transitions live
- [ ] Persist to Postgres so state survives restart
- [ ] Visualize the full task tree with handoff traces per node

### Demo

Submit "analyze the EV market in Europe". Open the SSE stream. Watch Triage split into four subtasks. Watch each child run its own Triage → Research → Synth handoff chain concurrently. Watch the parent wake up and run its own synthesis pass over the four child reports. Open the tree endpoint to see the full graph.

### Reflection

> "We now have recursion without recursion in the code. The queue is the recursion. The agents never call each other directly — they enqueue work and yield. This is how real distributed agent systems are built."

---

## Phase 6 — Skills: Portable Expertise

**Concept introduced:** Domain-shaped output via loadable bundles

### Why

The synthesizer writes generic reports. A finance analyst and a legal researcher produce very different deliverables from the same findings. Skills let us package that expertise as swappable bundles — and crucially, onboard a new domain expert without touching C# code.

### Concept

A skill is a folder containing a `SKILL.md` (instructions), optional few-shot example reports, optional helper scripts, and optional templates. The agent loads skills dynamically based on the task. Skills are the difference between "an LLM with tools" and "an LLM with a profession".

### Todos

- [ ] Define the skill folder contract: `SKILL.md` with frontmatter (name, description, trigger)
- [ ] Build a `SkillLoader` that scans `/skills` at startup
- [ ] Build a `SkillSelector` that matches a task to the best skill (LLM call or rules)
- [ ] Inject selected skill's `SKILL.md` into `SynthesizerAgent` system prompt
- [ ] Author skill: `finance-analyst` (citations-heavy, tables, exec summary on top)
- [ ] Author skill: `competitive-analysis` (positioning grid, SWOT, recommendations)
- [ ] Author skill: `academic-review` (methodology section, literature grounding)
- [ ] Skill can include example reports — few-shot by file reference, not inline
- [ ] Let users force a skill via the `POST /tasks` body
- [ ] Make skill selection itself a handoff decision — Triage picks the skill when splitting

### Demo

Take one finished task's findings. Re-synthesize three times with three skills loaded. Same data, three profession-shaped reports. The wow moment: add a new skill folder on disk, restart, it works — no code change. Then show Triage autonomously selecting the right skill based on task wording.

### Reflection

> "A skill is a prompt with structure around it. Once you have the loader, you can onboard a new domain expert by writing Markdown, not C#. That is a leverage point."

---

## Phase 7 — MCP: Tools as Infrastructure

**Concept introduced:** Model Context Protocol

### Why

Our external tools live inside the API. That works, but it does not compose. MCP is the protocol that lets tools run as separate processes that any MCP-aware agent can consume. It is how we turn `WebSearchPlugin` from a library into infrastructure. Note we do this AFTER handoffs — so learners can see that external tools and control-flow tools are the same primitive, some of which happen to cross a process boundary.

### Concept

MCP separates tool definition from tool caller. A server advertises tools over a standard protocol. Any client — Claude Desktop, our API, someone else's agent — can connect and use them. Same tools, many consumers, one source of truth. Handoff tools stay in-process (they belong to the orchestrator); world-facing tools move to MCP.

### Todos

- [ ] Extract `WebSearchPlugin` into a standalone MCP server project
- [ ] Expose `search` and `fetch` over MCP stdio transport
- [ ] Add MCP client in the main API that connects to the server
- [ ] Prove parity: existing tests pass with MCP-backed tools
- [ ] Verify handoff tools stay in-process — they are NOT externalized
- [ ] Author a second MCP server: `internal-docs` (searches a local corpus)
- [ ] Show `ResearchAgent` using both MCP servers in one task
- [ ] Test the MCP server from Claude Desktop to prove it is truly portable
- [ ] Document the tool contract — name, description, schema — as public API
- [ ] Graceful degradation when an MCP server is unreachable

### Demo

Run a task. Kill the MCP server mid-task. Watch graceful degradation. Restart it. Connect the same server from Claude Desktop and call the tool manually — same server, different client. This is the "aha" moment for MCP: tools outlive the app they were born in.

### Reflection

> "A tool inside your app serves one agent. A tool on MCP serves every agent you and your team will ever build. The cost to convert is small; the leverage compounds."

> "Note what did NOT move to MCP: the handoff tools. Those belong to the orchestrator because they reference in-process state. The distinction between ambient world-facing tools and process-internal control tools is a real architectural line."

---

## Phase 8 — Quality: Evals & Observability

**Concept introduced:** Making the system legible

### Why

The system works. Now we need to know how well, at what cost, and how to keep it that way as prompts and models change. With handoffs, we also need to see the graph each task traversed — that graph is the richest debugging artifact the system produces.

### Todos

- [ ] OpenTelemetry traces: one span per task, nested spans per agent call and tool call
- [ ] Metrics: queue depth, task duration, tokens per agent, tool call counts, hops per task
- [ ] Handoff graph visualization per task — show source/target/reasoning for each edge
- [ ] Cost dashboard: tokens × model price rolled up per root task
- [ ] Eval harness: fixed prompt set, scored with a judge model
- [ ] Snapshot tests for agent outputs — catches prompt regressions
- [ ] Eval the handoff behavior specifically: does Triage correctly route borderline tasks?
- [ ] Load test: queue 100 concurrent root tasks, measure throughput and failure rate
- [ ] Alert when re-triage rate exceeds ~5% — signals poor initial decomposition
- [ ] Prompt safety checks: length caps, disallowed-content filters
- [ ] Health checks: `/health/live`, `/health/ready` (DB + queue + MCP servers reachable)

### Demo

Open the observability dashboard. Submit three tasks of different complexity. Show the handoff graph for each. Change a prompt. Run the eval suite. Watch scores move. Merge only if eval scores improve — this is the start of a real prompt development workflow.

### Reflection

> "Without evals, prompt engineering is guessing. With evals, it is engineering. The handoff graph is a bonus: you can see not just whether the task succeeded, but how the agents collaborated to get there."

---

## Phase 9 — Deploy: Ship It

**Concept introduced:** Production reality

### Why

A demo on localhost teaches nothing about running LLM systems in production. This phase ships the system somewhere real and automates everything around it.

### Todos

- [ ] Multi-stage Dockerfile for the API
- [ ] Separate container for each MCP server
- [ ] docker-compose for local dev (api + postgres + mcp servers)
- [ ] Pulumi or Bicep stack for Azure: Container Apps, Postgres Flexible Server, Key Vault
- [ ] GitHub Actions: build → test → eval → container push → deploy
- [ ] Eval gate: block merge if eval scores regress beyond threshold
- [ ] Secrets in Key Vault, never in env vars in prod
- [ ] Environment promotion: dev → staging → prod with manual approval on prod
- [ ] One-command teardown script for learners who fork the repo
- [ ] Runbook: common failures and recovery steps (including handoff loops)

### Demo

Push a prompt change to a PR. Watch CI run tests + evals. Merge. Watch it deploy to staging. Promote to prod. Submit a real task against the public URL.

### Reflection

> "The interesting work in LLM systems is not the model call. It is everything around the model call that makes it safe, cheap, observable, and improvable. You just built all of that — and the agents decide their own flow inside the walls you built."

---

## How to Teach This

### Pacing

Each phase is roughly one workshop session (2–3 hours) or one week of self-paced work. Phases 1, 2, 4, and 7 are the four "concept" peaks — budget extra discussion time for those. Phase 4 (handoffs) is the headline — consider splitting it into two sessions if the group is new to agent patterns.

### What to preserve as teaching artifacts

- Every prompt version (v1 → vN) kept in the repo, never deleted.
- Bad-output examples from Phase 1, committed with a "before" label.
- Trace captures from Phase 2 showing tool-call loops in action.
- Handoff graph screenshots from Phase 4 — the headline visual.
- Before/after tool descriptions from Phase 2 to show description-as-prompt.
- The eval suite results over time — a visible quality graph.

### The arc learners walk away with

They start thinking "LLMs answer questions". They end thinking in terms of prompts as specifications, tools as a single primitive that does two jobs (world-facing and graph-facing), skills as packaged expertise, MCP as the connective tissue between systems, and evals as the only honest way to know any of it is working. The handoff pattern is the bridge that makes all of these feel like parts of one design, not separate techniques. That mental model transfers to every agent project they will ever touch.

---

## Notes for Implementation (for Claude Code)

A few things worth flagging before starting:

- `MAX_HOPS = 6` is a starting guess. Tune once real traces exist.
- The re-triage rate threshold of ~5% is also a guess; may be more useful as a relative metric than absolute gate.
- Phase 6's todo about Triage selecting the skill autonomously is ambitious — can be deferred to a Phase 6.5 if it feels like too much for one phase.
- Start every phase by committing a `PHASE_N.md` in the repo with the "Why/Concept/Demo/Reflection" sections copied in — it doubles as a living design doc for that phase.
- Keep old prompt versions in the repo forever. They are the teaching artifact.