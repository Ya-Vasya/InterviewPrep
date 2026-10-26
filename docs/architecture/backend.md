# Backend architecture

Structure of `backend/src`. See the [overview](overview.md) for principles and order of work, and [ADR 0002](../adr/0002-layered-backend-in-four-projects.md) for why it is split this way.

Status: **implemented** for R1 (questions), R2 (study) and R3 (AI check, explain, generate, notes). `Plan` and `Progress` (R4) are not built yet.

## Approach

Four projects, one per layer. A request flows `Controller -> Service -> Repository -> Database`; each layer only calls the one below it.

```
InterviewPrep.Api            -> Services, Infrastructure
InterviewPrep.Services       -> Infrastructure, Data
InterviewPrep.Infrastructure -> Data
InterviewPrep.Data
```

```
src/
  InterviewPrep.Api/
    Program.cs                 # composition only: AddX() calls, middleware, MapControllers()
    Controllers/               # thin: call one service method, return IActionResult
    Middleware/                # ApiExceptionHandler: typed exceptions -> ProblemDetails
    appsettings.json           # connection string, CORS, Paging, Study, Ai, Anthropic
  InterviewPrep.Services/
    Services/                  # IQuestionService, IStudyService, INoteService, IAiUsageService,
                               #   IAnswerCheckService, IQuestionGenerationService + implementations
    Dtos/                      # requests, responses, PagedResponse<T>, ToResponse() mapping
    Exceptions/                # ValidationException, NotFoundException, ConflictException, Ai*Exception
    Helpers/                   # EnumParsing, Paging, PromptText, StudyDay, ValidationErrors
    Rules/                     # QuestionTags, QuestionTexts, QuestionValidator, NoteLimits
    Prompts/                   # prompt building and parsing for check / explain / generate
    Scheduling/                # IScheduler + Sm2Scheduler (pure, no EF)
    Extensions/                # AddServices()
  InterviewPrep.Infrastructure/
    Options/                   # StudyOptions, PagingOptions, AiOptions, AnthropicOptions
    Ai/                        # IAiProvider, AnthropicAiProvider, NotConfiguredAiProvider
    Extensions/                # AddConfigurationOptions, AddDatabase, AddRepositories, AddAiProvider
  InterviewPrep.Data/
    Models/                    # entities (never serialized) and Models/Enums
    Repositories/              # I...Repository + EF implementations, IUnitOfWork
    Configurations/            # IEntityTypeConfiguration<T> per entity
    Migrations/
    SeedData/questions.json    # copied to the output folder, read by DatabaseInitializer
    AppDbContext.cs, DatabaseInitializer.cs
```

## Decisions

### Layers and what each may know

| Layer | Knows | Does not know |
|-------|-------|---------------|
| Controller | services, DTOs, HTTP attributes | repositories, EF, business rules |
| Service | repositories, options, the AI provider, DTOs | HTTP, `IActionResult`, `AppDbContext` |
| Repository | `AppDbContext`, entities, enums | DTOs, options, HTTP |

### Errors

A service returns a DTO or throws. `ApiExceptionHandler` maps the exception to ProblemDetails: `ValidationException` 400 (with `errors`), `NotFoundException` 404, `ConflictException` 409, `AiNotConfiguredException` 501, `AiDailyCapReachedException` 429, `AiInvalidOutputException` 502, `AiProviderException` 503 (+ `Retry-After`). Partial success (`accept` drafts) and idempotent replays (same `ReviewId`) are normal return values. `UseStatusCodePages()` still turns empty routing errors (404, 405) into ProblemDetails.

### Data access

Repositories return entities; for counts they return scalars or dictionaries (`CountByTopicAsync`), so grouping stays in SQL. They never save. Services call `IUnitOfWork.SaveChangesAsync()` once per use case. `AppDbContext` is no-tracking by default; repository methods that need tracking say so (`GetCardForUpdateAsync`, `GetReviewForUndoAsync`). Tests run against real PostgreSQL (Testcontainers) through `WebApplicationFactory`, not against mocked repositories.

### Models and DTOs

Entities (`Data/Models`) are never serialized. DTOs (`Services/Dtos`) are the API contract that `frontend/src/api/client.ts` mirrors, and are mapped with hand-written `ToResponse()` extension methods next to them. Enums live in `Data/Models/Enums` because both layers use them; they are persisted as integers and serialized as strings.

### Helpers

A helper is stateless, knows no business rule and has no dependencies. Rules about questions (`QuestionTags`, `QuestionTexts`, `QuestionValidator`) are in `Rules/`, not `Helpers/`.

### Configuration

Everything tunable is in `appsettings.json` and bound to options classes with `ValidateOnStart`; the classes carry no default values. Constants that are part of the API contract stay in code (`QuestionLimits`, `NoteLimits`, `CheckAnswerLimits`, `GenerateQuestionsLimits`).

### Logic isolated from EF

| Component | Role |
|-----------|------|
| `IScheduler` / `Sm2Scheduler` | Takes card state, rating and `TimeProvider`; returns new state. Pure. |
| `ProgressCalculator` (R4) | Takes plain records; returns metrics. Pure. |
| `IAiProvider` | The only external dependency. Prompt construction is a separate pure class (`Prompts/`) so the prompt-injection tests are cheap. |

### Validation

Hand-written, collected through `ValidationErrors` (`Add`, `AddNotOneOf`, `TryParseRequired`, `RequireZone`) and raised with `ThrowIfAny()`. Per-feature rules live in validator classes in `Rules/`. FluentValidation is not worth it at this size.

### Concurrency

Reviews are idempotent through the unique `ReviewLog.ReviewId` index. A lost race surfaces as `UniqueConstraintViolationException` from the unit of work, and the service retries once.

## Testing

- One `WebApplicationFactory<Program>` fixture, but **each test class gets its own database** inside one shared PostgreSQL Testcontainer.
- Plain unit tests exist for the scheduler, validation helpers and normalization rules.
- The AI provider (`FakeAiProvider`) and the clock are always faked; tests never call the real API.
- `ApiContractTests` snapshots `/openapi/v1.json`.

## When to extract further

When a part outgrows its folder (probably AI or progress), extract it then, not before.
