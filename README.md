# Interview Prep

A web app for preparing for .NET developer interviews.

```
InterviewPrep/
├── backend/    ASP.NET Core Web API (.NET 10) + xUnit tests
└── frontend/   React 19 + TypeScript + Vite
```

## Planned features

- Questions by topic: C#, ASP.NET Core, SQL, System Design
- Flashcards with spaced repetition
- AI that generates new questions, checks your answers and explains mistakes
- Progress tracker based on your study plan

Details, acceptance criteria and open questions: [docs/requirements.md](docs/requirements.md). Today only a small part of the first item exists (browsing seed questions by topic).

## Documentation

- [Requirements](docs/requirements.md): what the app should do
- [Architecture overview](docs/architecture/overview.md): target design, principles, order of work
  - [Backend](docs/architecture/backend.md)
  - [Frontend](docs/architecture/frontend.md)

## Prerequisites

- .NET 10 SDK — `dotnet --version`
- Node.js 20+ — `node -v`
- Docker — runs the PostgreSQL database (and is required by the backend tests)

## Run

Database (PostgreSQL on localhost:5432, data kept in the `pg-data` volume):

```bash
docker compose up -d db
```

Backend (http://localhost:5080):

```bash
cd backend
dotnet run --project src/InterviewPrep.Api
```

Frontend (http://localhost:5173), in a second terminal:

```bash
cd frontend
npm install
npm run dev
```

The Vite dev server proxies `/api/*` to the backend, so no extra config is needed.

## Test

```bash
cd backend
dotnet test
```

## API

| Method | Route                  | Description                        |
|--------|------------------------|------------------------------------|
| GET    | `/api/health`          | Health check                       |
| GET    | `/api/topics`          | The four topics with question counts |
| GET    | `/api/questions`       | Questions; optional `topic`, `difficulty`, `tag` filters (AND) |
| GET    | `/api/questions/{id}`  | One question                       |
| POST   | `/api/questions`       | Add a question                     |

OpenAPI document (Development only): http://localhost:5080/openapi/v1.json

Data is stored in PostgreSQL (`ConnectionStrings:Default`, default `Host=localhost;Port=5432;Database=interviewprep;Username=postgres;Password=postgres`, matching the `db` service in `docker-compose.yml`). Migrations are applied and seed questions loaded on startup. Run `docker compose down -v` to wipe the data.

The backend tests start a throwaway PostgreSQL container with Testcontainers, so Docker must be running; they do not touch the dev database.
