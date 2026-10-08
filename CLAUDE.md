# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Interview Prep: a web app for preparing for .NET developer interviews. Two independent projects in one repo (not a git repository yet):

- `backend/` — ASP.NET Core Web API (.NET 10, minimal APIs, EF Core + PostgreSQL) with xUnit integration tests
- `frontend/` — React 19 + TypeScript + Vite

Requires the .NET 10 SDK, Node.js 20+ and Docker (PostgreSQL for the dev database and for the tests).

Product requirements (topics, flashcards + spaced repetition, AI features, study-plan progress tracker) live in `docs/requirements.md`. Read it before building any of those features; it describes planned behavior of which only the R1 backend (topics, filters, tags, PostgreSQL) is implemented, so the Architecture section below remains the description of what exists.

The proposed target architecture (feature folders, EF Core + PostgreSQL, TanStack Query, routing) is in `docs/architecture/overview.md`, with details in `docs/architecture/backend.md` and `docs/architecture/frontend.md`. The backend already follows it (`Features/`, `Data/`, `Common/`); the frontend does not yet. Follow it when adding features, routing or new feature folders.

## Commands

Backend (run from `backend/`):

```bash
docker compose up -d db                          # PostgreSQL on localhost:5432 (run from the repo root)
dotnet run --project src/InterviewPrep.Api      # http://localhost:5080 (Development env via launchSettings)
dotnet test                                      # all tests (starts a PostgreSQL Testcontainer, needs Docker)
dotnet test --filter "FullyQualifiedName~CreateQuestion_ThenGetById_ReturnsIt"   # single test
```

Frontend (run from `frontend/`):

```bash
npm install
npm run dev      # http://localhost:5173
npm run build    # tsc -b && vite build (this is the only type-check; there is no lint or test script)
```

OpenAPI document is served only in Development at `http://localhost:5080/openapi/v1.json`. `src/InterviewPrep.Api/InterviewPrep.Api.http` has sample requests.

EF Core migrations (the `dotnet-ef` local tool is in `backend/dotnet-tools.json`; run `dotnet tool restore` once). Never edit migration files by hand:

```bash
dotnet ef migrations add <Name> --project src/InterviewPrep.Api --output-dir Data/Migrations
```

## Docker

Each project has its own `Dockerfile` (build context is the project folder); `docker-compose.yml` at the repo root runs the `db` (PostgreSQL), `backend` and `frontend` services.

```bash
docker compose up --build        # site at http://localhost:8080
docker build -t interviewprep-api backend
docker build -t interviewprep-web frontend
```

- Backend image: multi-stage .NET 10 SDK -> ASP.NET runtime, listens on 8080, runs non-root. The connection string comes from `ConnectionStrings__Default` (set in compose to the `db` service; the password defaults to `postgres`, override with `POSTGRES_PASSWORD`); the data lives in the `pg-data` volume. The backend waits for the `db` health check.
- Frontend image: Node build -> nginx. `nginx.conf.template` proxies `/api/` to `BACKEND_URL` (default `http://backend:8080`) and falls back to `index.html` for SPA routes, mirroring the Vite dev proxy. Keep the client's relative `/api/...` paths.
- Containers run in Production, so the OpenAPI document is not served, and CORS is not needed because the browser only talks to nginx.

## Architecture

**Backend** (`backend/src/InterviewPrep.Api`): vertical slices, no controllers. `Program.cs` is composition only: `AddPersistence()`, `InitializeDatabaseAsync()` (applies migrations, then seeds), and `MapQuestionEndpoints()`.

- `Common/` — `Topic` (`CSharp`, `AspNetCore`, `Sql`, `SystemDesign`) and `Difficulty` enums, plus `EnumParsing`. `Topic.DisplayName()` gives the UI name ("C#"). Enum values are persisted as integers; do not renumber.
- `Data/` — `AppDbContext` (PostgreSQL via Npgsql, no-tracking by default), `Configurations/` (`IEntityTypeConfiguration<T>` per entity), `Migrations/`, `SeedData/questions.json` (loaded once, when no `Source = Seed` rows exist), `DatabaseInitializer`.
- `Features/Questions/` — `Question` entity (never serialized), DTOs (`QuestionResponse`, `CreateQuestionRequest`, `TopicSummary`), and the `/api` endpoint group with inline handlers and hand-written validation (`Results.ValidationProblem`). There is no repository: handlers use `AppDbContext` directly.
- Enums are serialized as strings via a `JsonStringEnumConverter` registered in `Program.cs`. Request bodies and query filters take topic/difficulty as strings and parse them with `EnumParsing.TryParseName` so unknown values return a validation problem (400). Tests must register the same converter on their `JsonSerializerOptions`, and the frontend `Topic`/`Difficulty` types are the matching string unions.
- Tags are free-form, stored lowercase as a `text[]` column (EF primitive collection); the `tag` filter is case-insensitive. Questions created through the API have `Source = Manual`.
- CORS allowed origins come from the `Cors:AllowedOrigins` config section. In dev the browser never needs CORS because Vite proxies instead.
- `public partial class Program { }` at the bottom of `Program.cs` exists so tests can use `WebApplicationFactory<Program>`. Tests are end-to-end over HTTP against the real app. Use `ApiFactory` as the class fixture: one PostgreSQL Testcontainer is shared by the whole run and each test class gets its own freshly created, migrated and seeded database in it, so tests in different classes cannot affect each other; within a class they share it.

**Frontend** (`frontend/src`): `api/client.ts` is the only place that calls the backend, using relative `/api/...` paths. `vite.config.ts` proxies `/api` to `http://localhost:5080`, so the backend must be running for the UI to show data. The `Question`, `Topic`, `TopicSummary` and `Difficulty` types in `client.ts` are hand-maintained mirrors of the C# DTOs in `backend/.../Features/Questions/`; update both sides together. The client currently only exposes the GET endpoints (no create).
