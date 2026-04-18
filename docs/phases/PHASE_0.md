# Phase 0 — Foundation

**Concept introduced:** Project shape (no AI yet)

## Why

Before we wire up an LLM, we need somewhere for it to live. This phase gives learners a bare-bones API and a mental model of the codebase so later AI concepts drop into familiar slots.

## Concept

A plain ASP.NET Core minimal API. No intelligence. Just the skeleton — endpoints, a repository behind an interface, structured logging — so every later concept has a named slot to drop into.

## Todos

- [x] Scaffold ASP.NET Core minimal API (net10.0 — net9.0 SDK not installed locally)
- [x] Solution layout: `Api`, `Core`, `Agents`, `Infrastructure`, `Tests`
- [x] Add Semantic Kernel packages but do not use them yet
- [x] `POST /tasks` that stores a prompt and returns an ID
- [x] `GET /tasks/{id}` that returns the stored prompt
- [x] In-memory repository behind an `ITaskRepository` interface
- [x] Serilog with structured logging and a `taskId` correlation field
- [x] User-secrets wired

## Demo

```
curl -X POST http://localhost:5080/tasks \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"what is the current price of gold"}'
# => { "id": "..." }

curl http://localhost:5080/tasks/<id>
# => { "id":"...", "prompt":"what is the current price of gold", "kind":null }
```

Boring — and that is the point.

## Reflection

> "Notice there is no intelligence here yet. Everything that follows is added at specific, named places in this skeleton. The shape of the app does not change — only what happens inside one method."

## Calibration notes

- Target framework: `net10.0` (roadmap called for `net9.0`; revisit if 9 SDK gets installed).
- HTTP port: `5080` (5000 collides with macOS AirPlay Receiver; 5085 was the template default).
