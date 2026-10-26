# Architecture overview

Target architecture for Interview Prep. It describes where the code is **going**, to support the features in [requirements.md](../requirements.md). What exists today is described in [CLAUDE.md](../../CLAUDE.md) and [README.md](../../README.md).

Status: **partly implemented** (R1 backend and R2 are built; see the Status line in each linked document).

- [Backend architecture](backend.md)
- [Frontend architecture](frontend.md)

## Principles

- **Backend: organize by layer; frontend: by feature.** The backend is four projects (Api, Services, Infrastructure, Data) with the dependency direction `Api -> Services -> Infrastructure -> Data` (see [ADR 0002](../adr/0002-layered-backend-in-four-projects.md)). The frontend keeps one folder per requirement (questions, study, AI, plan, progress), and service and component names still follow the requirement names.
- **Keep the layering plain.** Controller, service, repository: no CQRS, no generic repository, no mediator. A repository exists per aggregate and returns entities.
- **Isolate what needs unit tests or is external:** the spaced-repetition scheduler, the progress calculator and the AI provider.
- **The API contract is the DTOs.** Backend records and `frontend/src/api/types.ts` mirror each other; entities and UI state are separate.

## Cross-cutting decisions

- **Monorepo, no shared code.** C# and TypeScript types stay separate and are tied together by the OpenAPI document.
- **Dev flow:** keep the Vite proxy (`/api` to `http://localhost:5080`), so the browser never needs CORS in development.
- **Production:** the ASP.NET app serves the built frontend (`UseStaticFiles` + `MapFallbackToFile("index.html")`), giving one process and no CORS.

## Suggested order of work

Same order as "Suggested delivery order" in [requirements.md](../requirements.md), with the structural work first.

1. Add PostgreSQL + `AppDbContext` and the `Topic` enum (R1), then split the backend into layers (see [backend.md](backend.md#data-access)).
2. Frontend: add TanStack Query and React Router, and split `App.tsx` into `features/questions` (see [frontend.md](frontend.md)).
3. Build `Study` with `Sm2Scheduler` and its unit tests, then the study UI (R2).
4. Build `Plan` + `Progress`, then the dashboard (R4).
5. Build `Ai`, starting with answer checking (R3).
