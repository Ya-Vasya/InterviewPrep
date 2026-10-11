# Backend architecture

Target structure for `backend/src/InterviewPrep.Api`. See the [overview](overview.md) for principles and order of work.

Status: **partly implemented**. `Common/`, `Data/` (PostgreSQL, migrations, seed) and `Features/Questions/` exist and the repository was dropped. `Study`, `Ai`, `Plan` and `Progress` are not built yet.

## Approach

Keep **one project** and use **vertical slices** (feature folders). Do not split into Domain / Application / Infrastructure projects: for a single-user app with four features that adds ceremony and no benefit.

```
src/InterviewPrep.Api/
  Program.cs                  # composition only: AddX() / MapX() calls
  Common/                     # Topic/Difficulty enums, validation helpers, TimeProvider setup
  Data/
    AppDbContext.cs           # PostgreSQL (Npgsql), EF Core
    Configurations/           # IEntityTypeConfiguration<T> per entity
    Migrations/
    SeedData/                 # seed questions as JSON, loaded by a startup seeder
  Features/
    Questions/                # R1: entity, endpoints, request/response DTOs, validation
    Study/                    # R2: Card, ReviewLog, endpoints, StudyQueueBuilder
      Scheduling/             # IScheduler + Sm2Scheduler (pure, no EF)
    Ai/                       # R3: IAiService, AnthropicAiService, prompts, schemas, AnswerAttempt
    Plan/                     # R4: StudyPlan entity, endpoints
    Progress/                 # R4: ProgressCalculator (pure) + endpoint
```

Each feature exposes `AddXFeature(this IServiceCollection)` and `MapXEndpoints(this IEndpointRouteBuilder)`, so `Program.cs` stays a short list. This extends the existing `MapQuestionEndpoints()` convention.

## Decisions

### Data access

Use `AppDbContext` directly in handlers and **drop `IQuestionRepository`** when moving to EF Core. The repository exists only because the data is in memory. Over EF it hides `Include`, projections and the queue/progress queries. Tests run against a real PostgreSQL (Testcontainers) through `WebApplicationFactory`, which is more faithful than mocking a repository.

### Logic isolated from EF

Only three things are isolated, and all three are named in the requirements:

| Component | Role |
|-----------|------|
| `IScheduler` / `Sm2Scheduler` | Takes card state, rating and `TimeProvider`; returns new state. Pure. |
| `ProgressCalculator` | Takes plain records (cards, logs, plan, "today"); returns metrics. Pure. |
| `IAiService` | The only external dependency. Prompt construction lives in a separate pure class so the prompt-injection test is cheap. |

### Entities vs DTOs

Request/response records are the API contract (what `frontend/src/api/types.ts` mirrors). EF entities are separate classes. Never serialize entities directly, especially once `Card` and `ReviewLog` have navigation properties.

### Validation

Keep hand-written validation returning `Results.ValidationProblem`. Extract a small helper (`Errors.Add("topic", "...")`, `.ToProblem()`) once there are more than two endpoints. FluentValidation is not worth it at this size.

### Cross-cutting setup

- `TimeProvider.System` as a singleton; tests swap in `FakeTimeProvider`.
- `IOptions<AiOptions>` with `ValidateOnStart`.
- Keep the global `UseExceptionHandler` + ProblemDetails already in `Program.cs`.
- AI calls go through `IHttpClientFactory` with a timeout; the daily cap is a small separate service.

### Concurrency

The double-click requirement (R2) needs an idempotency key or a "reviewed since" check in the review endpoint. A `RowVersion` or `lastReviewedAt` comparison on `Card` is enough for PostgreSQL (or map `xmin` as the concurrency token).

## Testing

- One `WebApplicationFactory<Program>` fixture, but **each test class gets its own database** inside one shared PostgreSQL Testcontainer. This removes the "tests can affect each other" caveat in CLAUDE.md.
- Add a `Unit/` folder for scheduler and progress tests (plain unit tests with a fake clock).
- The AI provider and the clock are always faked; tests never call the real API.

## When to extract further

When a feature outgrows its folder (probably `Ai` or `Progress`), extract it then, not before.
