# Architecture overview

Target architecture for Interview Prep. It describes where the code is **going**, to support the features in [requirements.md](../requirements.md). What exists today is described in [CLAUDE.md](../../CLAUDE.md) and [README.md](../../README.md).

Status: **proposed**, not yet implemented.

- [Backend architecture](backend.md)
- [Frontend architecture](frontend.md)

## Principles

- **Organize by feature, not by technical layer.** Each requirement (questions, study, AI, plan, progress) gets its own folder on both the backend and the frontend, and the names match on both sides.
- **Add structure only where logic needs isolating.** Single user, four features: no Clean/Onion project split, no CQRS, no generic repository.
- **Isolate what needs unit tests or is external:** the spaced-repetition scheduler, the progress calculator and the AI provider.
- **The API contract is the DTOs.** Backend records and `frontend/src/api/types.ts` mirror each other; entities and UI state are separate.

## Cross-cutting decisions

- **Monorepo, no shared code.** C# and TypeScript types stay separate and are tied together by the OpenAPI document.
- **Dev flow:** keep the Vite proxy (`/api` to `http://localhost:5080`), so the browser never needs CORS in development.
- **Production:** the ASP.NET app serves the built frontend (`UseStaticFiles` + `MapFallbackToFile("index.html")`), giving one process and no CORS.
- **Version control:** the repo is not under git yet. Initialize it before starting the restructure below, since it touches many files.

## Suggested order of work

Same order as "Suggested delivery order" in [requirements.md](../requirements.md), with the structural work first.

1. Add SQLite + `AppDbContext`, move to `Features/` folders, add the `Topic` enum (R1). This is the point to drop `IQuestionRepository` (see [backend.md](backend.md#data-access)).
2. Frontend: add TanStack Query and React Router, and split `App.tsx` into `features/questions` (see [frontend.md](frontend.md)).
3. Build `Study` with `Sm2Scheduler` and its unit tests, then the study UI (R2).
4. Build `Plan` + `Progress`, then the dashboard (R4).
5. Build `Ai`, starting with answer checking (R3).

## Open decision

[requirements.md](../requirements.md) says persistence sits "behind the existing repository-style interfaces". This proposal recommends dropping `IQuestionRepository` and using `AppDbContext` directly in handlers. If that is accepted, update that line in the requirements.
