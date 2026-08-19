# Agent Skills for Claude Code

A skill pack for this repo's three areas — **backend** (`backend/`, .NET Clean Architecture template), **frontend** (`frontend/`, Angular static SPA + Cloudflare Worker), and **infra** (`infra/`, OpenTofu provisioning the Kubernetes cluster on the Raspberry Pis) — so every feature Claude Code builds looks like you wrote it.

Four of the five skills below are **backend-only**; `add-frontend-feature` is the frontend counterpart. There's no dedicated skill for `infra/` yet — it changes rarely enough to not need one.

## What's inside

| Skill | Area | Invoke with | What it does |
|---|---|---|---|
| **add-feature** | backend | `/add-feature archive a todo item` | Scaffolds a complete vertical slice: command/query, handler, validator, endpoint, and unit + validator + integration tests. |
| **add-entity** | backend | `/add-entity Project with a name and owner` | Adds a domain entity end to end: entity, error catalog, domain events, EF configuration, DbContext wiring, migration. |
| **add-tests** | backend | `/add-tests CopyTodoCommand` | Backfills handler, validator, and integration tests for existing use cases. |
| **ca-review** | backend | `/ca-review` | Reviews pending changes against the template's conventions: layer boundaries, error handling, security, caching, and test coverage. |
| **add-frontend-feature** | frontend | `/add-frontend-feature list todos` | Scaffolds an Angular feature: standalone component, HttpClient-backed service, route, and specs. |

You don't have to invoke them explicitly — once installed, Claude Code picks the right skill automatically when you say things like "add an endpoint to snooze a todo" or "add a todo list page."

## Installation

The skills live in `.claude/skills/`. If you cloned the template, they're already active — just open the repo in Claude Code.

To use them in another project based on this template, copy the folder:

```
your-project/
└── .claude/
    └── skills/
        ├── add-feature/
        ├── add-entity/
        ├── add-tests/
        ├── ca-review/
        └── add-frontend-feature/
```

The backend skills work with both the standard and the Aspire variants of the Clean Architecture template.

## Try it

```
/add-feature snooze a todo until a given date
```

Claude will create the command, validator, handler (with ownership check, domain event, and cache invalidation), the endpoint, and the three test types — then build and run the tests.

## Customizing

Each skill is a plain Markdown file (`SKILL.md`, plus templates under `references/`). Renamed your layers, prefer records everywhere, use a different test stack? Edit the templates once and every future feature follows suit. The skills are the executable version of your team's conventions doc.

---

Built for the [Clean Architecture template](https://www.milanjovanovic.tech) by Milan Jovanović.
