# Layered backend in four projects

The backend started as one project with vertical slices (`Features/Questions`, `Features/Study`, ...) where endpoint handlers used `AppDbContext` directly. We now split it by layer into four projects: `InterviewPrep.Api` (controllers, host), `InterviewPrep.Services` (business logic and DTOs), `InterviewPrep.Infrastructure` (options, DI extensions, the AI provider adapter) and `InterviewPrep.Data` (entities, repositories, `AppDbContext`, migrations). The model is the layered architecture of the article this refactor followed: controller, service, repository, with models and helpers beside them. The HTTP contract is unchanged.

## Considered Options

- **Keep vertical slices.** Fewer files per feature, but handlers mix HTTP, rules and EF queries, and none of it can be unit tested without the database.
- **Layers as folders in one project.** Cheapest, but nothing stops a controller from reaching into `AppDbContext`.
- **Layers as projects (chosen).** The compiler enforces the direction `Api -> Services -> Infrastructure -> Data`.
- **`ApiResponse<T>` envelope on every response.** Rejected: it would rewrite every response body, the frontend types and the OpenAPI snapshot for no new capability. Errors stay ProblemDetails.
- **A `ServiceResult<T>` returned by services.** Rejected in favour of typed exceptions that one `IExceptionHandler` maps to HTTP: a service returns a DTO or throws.

## Consequences

- Services never see HTTP or `IActionResult`. They throw `ValidationException`, `NotFoundException`, `ConflictException` and the `Ai*` exceptions (`Services/Exceptions`); `AiProviderException` comes from Infrastructure.
- Repositories return entities, plus scalars and dictionaries for counts. They do not save: services call `IUnitOfWork.SaveChangesAsync()` once per use case, which keeps "card plus first review" atomic. A unique-index violation surfaces as `UniqueConstraintViolationException`.
- Tunable values live in `appsettings.json` and are bound to options classes in Infrastructure with no defaults in code. Constants that are part of the API contract (`QuestionLimits`, `NoteLimits`) stay in code.
- Seeding lives in Data and stores seed tags as written. `SeedDataTests` fails the build if the seed file is not already normalized.
- `dotnet ef` needs both projects: `--project src/InterviewPrep.Data --startup-project src/InterviewPrep.Api`.
