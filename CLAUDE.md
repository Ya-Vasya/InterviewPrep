# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Interview Prep: a web app for preparing for .NET developer interviews. Two independent projects in one repo (not a git repository yet):

- `backend/` — ASP.NET Core Web API (.NET 10, minimal APIs, EF Core + PostgreSQL) with xUnit integration tests
- `frontend/` — React 19 + TypeScript + Vite

Requires the .NET 10 SDK, Node.js 20+ and Docker (PostgreSQL for the dev database and for the tests).

Product requirements (topics, flashcards + spaced repetition, AI features, study-plan progress tracker) live in `docs/requirements.md`. Read it before building any of those features; it describes planned behavior of which the R1 backend (topics, filters, tags, PostgreSQL) and R2 (flashcards, spaced repetition, the `/study` pages) are implemented, so the Architecture section below remains the description of what exists. Domain vocabulary (Question vs Card, Rating vs Difficulty, Due, Review) is defined in `GLOSSARY.md`; use those terms in code and UI.

The proposed target architecture (feature folders, EF Core + PostgreSQL, TanStack Query, routing) is in `docs/architecture/overview.md`, with details in `docs/architecture/backend.md` and `docs/architecture/frontend.md`. The backend follows it (`Features/`, `Data/`, `Common/`) and the frontend follows it from routing and TanStack Query onwards (`app/`, `features/`, `components/`). Follow it when adding features, routing or new feature folders.

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
npm run build    # tsc -b && vite build (also type-checks the tests; there is no lint script)
npm test         # vitest run (jsdom + Testing Library)
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

**Backend** (`backend/src/InterviewPrep.Api`): vertical slices, no controllers. `Program.cs` is composition only: `AddPersistence()`, `AddStudyFeature()`, `InitializeDatabaseAsync()` (applies migrations, then seeds), `MapQuestionEndpoints()` and `MapStudyEndpoints()`.

- `Common/` — `Topic` (`CSharp`, `AspNetCore`, `Sql`, `SystemDesign`) and `Difficulty` enums, plus `EnumParsing`. `Topic.DisplayName()` gives the UI name ("C#"). Enum values are persisted as integers; do not renumber.
- `Data/` — `AppDbContext` (PostgreSQL via Npgsql, no-tracking by default), `Configurations/` (`IEntityTypeConfiguration<T>` per entity), `Migrations/`, `SeedData/questions.json`, `DatabaseInitializer`. Every seed entry has a stable `id` (GUID); cards and review logs reference questions by it, so never change an existing id. On every start the seeder inserts missing entries and refreshes the content of existing `Source = Seed` rows, so new seed questions reach existing databases. Give new entries a fresh GUID.
- `Features/Questions/` — `Question` entity (never serialized), DTOs (`QuestionResponse`, `CreateQuestionRequest`, `TopicSummary`), and the `/api` endpoint group with inline handlers and hand-written validation: handlers collect errors in `Common/ValidationErrors` (`Add`, `AddNotOneOf<TEnum>`, `TryParseRequired<TEnum>`, `ToProblem()`), and `QuestionValidator` holds the create rules. Tags go through `QuestionTags.Normalize/NormalizeAll` everywhere (API and seeder), so never trim or lowercase tags by hand. Handlers return `TypedResults` (`Results<Ok<T>, NotFound>` unions), not `IResult`, so the OpenAPI document describes every response; give each endpoint `.WithName(...)`. `UseStatusCodePages()` turns empty error responses (404, 405) into ProblemDetails, so every error body has the same shape. `GET /api/questions` returns a `PagedResponse<QuestionResponse>` (`items`, `total`, `limit`, `offset`; `limit` default 50, max 200, validated by `Common/Paging`) ordered with `Id` as the last tie-breaker so pages are stable. A list endpoint must never return a bare array: it could not gain `total` later without breaking the client. There is no repository: handlers use `AppDbContext` directly.
- `Features/Study/` (R2) — `Card` (a user's learning state for one question, unique per question, created lazily with its first rating), `ReviewLog` (append-only; unique `ReviewId` is the idempotency key, plus a `Before` snapshot of the card state for Undo), `Rating` (`Again`/`Hard`/`Good`/`Easy`, not the question's `Difficulty`), `StudyOptions` (`Study:NewCardsPerDay` 20, `Study:ReviewsPerDay` 200), `StudyDay` (day start in the browser's IANA time zone, sent as `tz`), and `StudyEndpoints` under `/api/study`: `GET summary`, `GET queue` (a pure read, creates no cards), `POST reviews`, `DELETE reviews/{reviewId}` (Undo; only a card's latest review; undoing a card's first review deletes the card), `POST cards/{id}/suspend|unsuspend`. `Scheduling/` holds `IScheduler` and `Sm2Scheduler(TimeProvider)`, pure with no EF. Timestamps are UTC `DateTime` (`timestamptz`; Npgsql rejects a non-UTC `DateTime` on write, so always use `TimeProvider.GetUtcNow().UtcDateTime`). A review that created its card counts against the new-card limit, any other review against the review limit.
- `Features/Ai/` (R3, check answers so far) — `IAiService` is the only thing feature code knows about the LLM: `CompleteJsonAsync(AiRequest)` returns JSON text and throws `AiProviderException` on provider failure. `AnthropicAiService` (official Anthropic SDK, one model from `Anthropic:Model`, default `claude-sonnet-5-5`, effort low, SDK retries off) is registered only when `Anthropic:ApiKey` is set; otherwise `NotConfiguredAiService` reports `IsConfigured = false`. Set the key with `dotnet user-secrets set "Anthropic:ApiKey" "<key>" --project src/InterviewPrep.Api` (development) or the `ANTHROPIC_API_KEY` env var that compose passes through. Every AI endpoint goes through `AiGateway`: 501 without a key, 429 when the daily cap (`Ai:DailyCap`, default 50, counted per browser-time-zone day from the `AiCalls` table) is used up, one retry on invalid model output (502 after the second), provider failures as 503 with `Retry-After`. Every call that reaches the provider is recorded first, so retries and failures count against the cap. `CheckAnswerPrompt` builds the prompt: instructions in the system prompt only, the user's answer in a delimited `<candidate_answer>` block with angle brackets escaped. Endpoints: `GET /api/ai/status` (`enabled`, `usedToday`, `dailyCap`; the UI hides AI controls when disabled), `POST /api/ai/answers/check` (stores an `AnswerAttempt`, returns the verdict and a suggested `Rating`; never rates a card), `GET /api/ai/answers/attempts?questionId=` (a `PagedResponse`, newest first). `POST /api/study/reviews` takes an optional `attemptId`: the server records `ReviewSource.Ai` only when that attempt is for the same question and the rating equals the suggestion. Structured output uses the provider's JSON-schema output, not forced tool calls (Sonnet 5.5 rejects them). Tests use a scriptable `FakeAiService` (`factory.Ai`) and never call the real API.
- Enums are serialized as strings via a `JsonStringEnumConverter` registered in `Program.cs`. Request bodies and query filters take topic/difficulty as strings and parse them with `EnumParsing.TryParseName` so unknown values return a validation problem (400). Tests must register the same converter on their `JsonSerializerOptions`, and the frontend `Topic`/`Difficulty` types are the matching string unions.
- Tags are free-form, stored lowercase as a `text[]` column (EF primitive collection); the `tag` filter is case-insensitive. Questions created through the API have `Source = Manual`.
- CORS allowed origins come from the `Cors:AllowedOrigins` config section. In dev the browser never needs CORS because Vite proxies instead.
- `public partial class Program { }` at the bottom of `Program.cs` exists so tests can use `WebApplicationFactory<Program>`. Tests are end-to-end over HTTP against the real app. Use `ApiFactory` as the class fixture: one PostgreSQL Testcontainer is shared by the whole run and each test class gets its own freshly created, migrated and seeded database in it, so tests in different classes cannot affect each other; within a class they share it. The app runs on a `FakeClock` (`factory.Clock`); study tests derive from `StudyTestBase`, which wipes cards and reviews and rewinds the clock before each test. To change options (daily limits), subclass `ApiFactory` and override `Settings` (see `StudyLimitTests.cs`). `Sm2SchedulerTests` are plain unit tests. `ApiContractTests` snapshots `/openapi/v1.json` with Verify (`*.verified.json`): when an endpoint or DTO changes on purpose the test fails and writes a `*.received.json`; review the diff, then replace the `.verified.json` with it and commit both the code and the snapshot. An unexpected diff is a contract change the frontend may not expect.

**Frontend** (`frontend/src`): `api/client.ts` is the only place that calls the backend, using relative `/api/...` paths. `vite.config.ts` proxies `/api` to `http://localhost:5080`, so the backend must be running for the UI to show data. The `Question`, `Topic`, `TopicSummary`, `Difficulty` and study types (`Rating`, `StudySummary`, `StudyQueueItem`, `ReviewResult`, ...) in `client.ts` are hand-maintained mirrors of the C# DTOs in `backend/.../Features/Questions/` and `Features/Study/`; update both sides together. The client has no create-question call. `api.getQuestions` returns a `Page<Question>` and `QuestionsPage` uses `useInfiniteQuery` ("Load more"). Failed requests throw `ApiError` (status, ProblemDetails message, field `errors` for validation failures).

- `main.tsx` provides TanStack Query and React Router (`app/router.tsx`, `app/Layout.tsx` nav shell). Routes: `/questions`, `/study` (setup screen: topic choice with due/new counts), `/study/session?topic=...` (the card session; the topic selection lives in the URL), `/` redirects to `/study`.
- `features/study/`: `StudySession` is a `useReducer` over the pure `sessionReducer` (deck, flip, rate, Again re-queue 3 cards later, Undo, failed-save rollback). The queue is fetched once per session and never refetched mid-session. A `reviewId` GUID is generated per showing of a card (`newGuid` works on plain http, where `crypto.randomUUID` does not exist). Rating buttons are not rendered until the card is flipped. `useStudyShortcuts`: Space = flip, 1-4 = rate. Animations are plain CSS in `study.css`; no swipe gestures.
- `features/ai/`: `useAiStatus` reads `GET /api/ai/status` (AI controls render only when `enabled`; the usage counter sits next to them, not in the nav). `AnswerCheckForm` is the optional "Your answer" box on a card's front: submitting calls `api.checkAnswer`, `StudySession` stores the result keyed by the card showing, flips the card and shows `CheckResultPanel`; `RatingButtons` highlights the suggested rating without applying it, and the rating request carries the `attemptId`. `AttemptHistory` lists earlier attempts on the card back. The textarea is a text field, so `useStudyShortcuts` ignores Space and 1-4 while typing.
- `components/Markdown.tsx` is the one place that renders answers: react-markdown plus `rehypeHighlightLite`, which uses lowlight with only the languages interview answers need (C#, SQL, JSON, XML, YAML, Bash, JS, TS). `rehype-highlight` is deliberately not used: it always bundles all 37 default languages. Add a language by importing it in `rehypeHighlightLite.ts`. Token colors are CSS variables in `index.css`. `/questions` and `/study/session` are lazy routes (`app/router.tsx`) so the markdown code stays out of the initial bundle; keep `StudySetupPage` free of imports from those pages.
- Tests: Vitest + Testing Library next to the code (`*.test.ts(x)`), mocking `api` from `api/client`.
