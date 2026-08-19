# Poker Game Manager

A monorepo with three parts:

- **`backend/`** — .NET 10 API (Clean Architecture template), backed by PostgreSQL.
- **`frontend/`** — Angular standalone app, built to static assets and served by a Cloudflare
  Worker that also proxies `/api/v1/*` to the API, so front and API share one domain in production.
- **`infra/`** — OpenTofu, provisioning the Kubernetes side (API + Postgres) on a k3s cluster
  running on 2 Raspberry Pi nodes at home. A Cloudflare Tunnel (already running on the Pis,
  managed outside this repo) connects the public Worker to the private cluster.

See each folder's own README for details. `.claude/skills/` has Claude Code skills for scaffolding
backend features/entities/tests and frontend features consistently — see
[.claude/skills/README.md](.claude/skills/README.md).

## What's in the backend

- **SharedKernel** project with common Domain-Driven Design abstractions.
- **Domain** layer with sample entities and domain events.
- **Application** layer with abstractions for:
  - CQRS (lightweight, MediatR-free command/query handlers)
  - Example use cases (Todos and Users)
  - Cross-cutting concerns (logging, validation) implemented as decorators
- **Infrastructure** layer with:
  - JWT authentication with **refresh tokens** (with token rotation)
  - Permission-based authorization
  - EF Core + PostgreSQL (snake_case naming, migrations)
  - **HybridCache** for fast, unified caching with cache invalidation
  - Serilog structured logging
- **Web.Api** layer with:
  - Minimal API endpoints, mounted under `/api/v1`
  - **CORS**, configurable via `Cors:AllowedOrigins` (only needed in dev — production is same-origin through the Worker)
  - **Rate limiting** (configurable global + authentication policies)
  - **OpenTelemetry** tracing and metrics (ASP.NET Core, HTTP, Npgsql, runtime)
  - Global exception handling and `ProblemDetails`
  - Swagger / OpenAPI with JWT support
- **Seq** for searching and analyzing structured logs
  - Seq is available at http://localhost:8081 by default
- **Testing** projects
  - Architecture testing (`ArchitectureTests`)
  - Unit testing (`Application.UnitTests`)
  - Integration testing with **Testcontainers** (`IntegrationTests`)

## Getting started

Backend:

```bash
docker compose up -d              # PostgreSQL + Seq (compose lives at repo root, builds backend/)
cd backend && dotnet run --project src/Web.Api
```

Run the backend test suite (integration tests spin up a throwaway PostgreSQL container, so
Docker must be running):

```bash
cd backend
dotnet test CleanArchitecture.slnx
```

Frontend (needs the backend running locally too — see `frontend/README.md`):

```bash
cd frontend
npm install
npm start                    # http://localhost:4200
```

To target .NET 8 or .NET 9 instead of .NET 10, see the notes in `backend/Directory.Build.props`.

If you're ready to learn more about the backend template, check out
[**Pragmatic Clean Architecture**](https://www.milanjovanovic.tech/pragmatic-clean-architecture?utm_source=ca-template):

- Domain-Driven Design
- Role-based authorization
- Permission-based authorization
- Distributed caching with Redis
- OpenTelemetry
- Outbox pattern
- API Versioning
- Unit testing
- Functional testing
- Integration testing
