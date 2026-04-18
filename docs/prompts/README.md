# Prompt Style Guide

This project treats prompts as specifications, not folklore. Every prompt lives in a versioned file under `/prompts`, and every change is a new version — we never overwrite or collapse versions into comments. Old versions are the teaching artifact.

## File layout

```
/prompts
  triage.v1.txt       — earliest versions, kept as-is for the v1→v4 teaching arc
  triage.v2.txt
  triage.v3.txt
  triage.v4.txt       — current triage version
  research.v1.txt     — Phase 2 baseline (plain prose, tool description lives here)
  research.v2.prompty — Phase 3 — XML delimiters, templated constraints
  research.v3.prompty — Phase 3 — adds step-by-step process block
  research.v4.prompty — Phase 3 — adds negative examples and do-not list
```

Naming: `<agent>.v<N>.<ext>`. The current version is selected by configuration (`Triage:PromptVersion`, `Research:PromptVersion`), not by convention. New versions of a prompt default to `.prompty`; `.txt` is the legacy format and stays in the repo as-is.

## The `.prompty` format

A `.prompty` file is YAML frontmatter followed by a template body.

```prompty
---
name: research
version: v2
description: XML-delimited sections, structured schema, templated constraints.
model: gpt-4o-mini
temperature: 0.2
---
<role>
You are a research assistant ...
</role>
```

### Frontmatter

- `name` — agent the prompt is for (e.g. `research`, `triage`).
- `version` — must match the filename suffix.
- `description` — one line describing what changed from the previous version. Read this before diffing.
- `model`, `temperature` — documentation of the intended runtime, not a runtime override (the agent factory is still authoritative). Useful for learners diffing versions.

The `PromptLoader` strips the frontmatter and returns the body. Frontmatter is for humans and tooling, not the model.

### Template variables

The body may contain `{{variable_name}}` placeholders. `ResearchAgentFactory` injects:

| Variable | Meaning |
| --- | --- |
| `{{max_tool_calls}}` | The tool-call budget the runtime enforces for this task. |
| `{{max_fetch_chars}}` | Truncation cap on `web.fetch` output. |
| `{{current_date}}` | Today's date (`yyyy-MM-dd`), rendered per invocation. |

Unknown placeholders are left intact — that way a missing variable fails loudly during model response, not silently.

## Patterns we use (and why)

Each pattern below shows up in one of the versioned files. The order roughly matches how learners encounter them walking from v1 to v4.

### 1. Role framing

Open with who the model is in this task. Not "you are helpful" — who it is *for this job*. See `research.v2.prompty`'s `<role>` block.

### 2. XML-delimited sections

Wrap each part of the prompt in named tags: `<role>`, `<tools>`, `<constraints>`, `<output_schema>`, `<examples>`. Benefits:

- The model attends to structure. Asking "per the constraints" is more reliable than hoping it remembers a bullet list.
- Diffs become readable — you can change `<constraints>` without reflowing prose.
- It's honest: you are writing a spec, not a conversation.

### 3. Explicit output schema

State the JSON shape in the prompt **even when** you enforce it at runtime with structured output. The runtime schema keeps the JSON valid; the in-prompt schema shapes what the model puts *inside* the JSON (claim granularity, confidence calibration, URL hygiene).

### 4. Chain-of-thought trigger (with care)

A `<process>` block that walks the model through how to decide its next tool call. Keep it action-oriented — "restate the question, decide search/fetch/answer, justify in one sentence" — not "think deeply." With structured output, the reasoning happens between tool calls, not in the final JSON.

### 5. Negative examples and `<do_not>` lists

Show what bad output looks like and explain *why* it is bad. A negative example with a one-line "why" is worth ten lines of abstract guidance.

### 6. Templated constraints

Numbers that matter (budgets, limits, dates) flow from configuration into the prompt via `{{vars}}`. If the tool-call budget changes in `appsettings.json`, the prompt changes too — otherwise the two drift and the model is working against stale instructions.

## When to make a new version

Make a new version when:

- You change wording or structure in a way that could move metrics.
- You add or remove a section.
- You change a constraint the model is supposed to obey.

Do *not* make a new version for pure typo fixes. Document those in the existing file's description if it matters.

## When not to use this format

- Tool descriptions (`[Description("...")]`) are prompts too, but they live on the C# function — not in `/prompts`. They version with the code.
- Inline system messages used once in a script or test can stay in code.
- Handoff descriptions (Phase 4) follow the same in-code pattern as tool descriptions.

## The rule you will break first

Resist the urge to edit `triage.v1.txt` to "make it better". Its job is to be bad on purpose. The v1 → v4 arc is how we teach that prompt iteration is cheap and effective — which only works if v1 stays visibly worse.
