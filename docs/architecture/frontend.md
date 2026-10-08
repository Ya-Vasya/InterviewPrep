# Frontend architecture

Target structure for `frontend/src`. See the [overview](overview.md) for principles and order of work.

Status: **proposed**. Today everything is in `App.tsx` and `api/client.ts`, with `useEffect` fetching and local `loading`/`error` state.

## Structure

```
frontend/src/
  main.tsx                    # providers: QueryClientProvider, RouterProvider
  app/
    router.tsx                # routes
    Layout.tsx                # nav shell
  api/
    http.ts                   # fetch wrapper: base path, ProblemDetails -> ApiError
    types.ts                  # Question, Difficulty, Topic, Card, ... (mirrors C# DTOs)
    questions.ts  study.ts  ai.ts  plan.ts  progress.ts   # one module per backend feature
  features/
    questions/                # QuestionList, QuestionCard, filters, AddQuestionForm
    study/                    # StudySession, FlashCard, RatingButtons, useStudyShortcuts
    ai/                       # AnswerChecker, MistakeExplanation, GenerateDialog
    dashboard/                # Dashboard, TopicBars, charts, PlanForm
  components/                 # shared UI only: Button, Select, Markdown, ErrorMessage, Spinner
  hooks/                      # useLocalStorageState, etc.
```

Feature folders mirror the backend features (`Features/Study` ↔ `features/study`). `api/` stays the only place that calls `fetch`.

## Decisions

| Area | Decision | Why |
|------|----------|-----|
| Server state | **TanStack Query** | Replaces hand-written `useEffect` + loading/error state; gives caching, invalidation (rate a card, then refetch queue and progress) and cancellation. Biggest single improvement. |
| Routing | **React Router**: `/questions`, `/study`, `/dashboard`, `/plan` | Needed once there is more than one screen. |
| Filter persistence | Store topic/difficulty in the **URL query string** (`?topic=SQL&difficulty=Senior`) | Satisfies "selection survives a reload" (R1) and gives shareable links without `localStorage`. |
| Local state | `useState` / `useReducer` only. No Redux or Zustand. | The study session's queue and reveal state is a `useReducer` inside `StudySession`; everything else is server state in Query. |
| Types | Hand-written in `api/types.ts` for now. | Once there are ~15+ DTOs, generate them from `/openapi/v1.json` (`openapi-typescript`) to remove the "update both sides" risk. |
| Markdown | `react-markdown` + a highlighter (`rehype-highlight` or `shiki`) in one shared `<Markdown>` component | Answers contain fenced C# and SQL code. |
| Charts | `recharts` | Enough for the reviews-per-day and mastery-over-time charts. |
| Styling | One approach only. Plain CSS modules is fine with no extra dependencies. | A component library (e.g. shadcn/ui) is worth it only if fast polish is wanted. |

## Tooling and tests

The project currently has no lint or test script; `npm run build` (`tsc -b`) is the only check.

- Add **ESLint** (the Vite React template config).
- Add **Vitest + Testing Library**, mainly for `useStudyShortcuts` (Space = reveal, 1–4 = rate) and the rating flow.
- Playwright only later, if one end-to-end smoke test is wanted.
