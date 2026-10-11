import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, type StudySummary } from '../../api/client'
import { selectionTotals, StudySetupPage } from './StudySetupPage'

vi.mock('../../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/client')>()),
  api: { getStudySummary: vi.fn() },
}))

const summary = (overrides: Partial<StudySummary> = {}): StudySummary => ({
  topics: [
    { topic: 'CSharp', name: 'C#', dueCount: 3, newCount: 10, nextDueAt: null },
    { topic: 'AspNetCore', name: 'ASP.NET Core', dueCount: 0, newCount: 10, nextDueAt: null },
    { topic: 'Sql', name: 'SQL', dueCount: 2, newCount: 10, nextDueAt: '2099-01-01T00:00:00Z' },
    { topic: 'SystemDesign', name: 'System Design', dueCount: 0, newCount: 10, nextDueAt: '2098-01-01T00:00:00Z' },
  ],
  newRemainingToday: 20,
  reviewsRemainingToday: 200,
  ...overrides,
})

function Location() {
  const { pathname, search } = useLocation()
  return <p data-testid="location">{pathname + search}</p>
}

function renderSetup() {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={['/study']}>
        <Location />
        <Routes>
          <Route path="/study" element={<StudySetupPage />} />
          <Route path="/study/session" element={<p>session</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return userEvent.setup()
}

beforeEach(() => vi.mocked(api.getStudySummary).mockReset())

describe('StudySetupPage', () => {
  it('lists the topics with due and new counts, all selected by default', async () => {
    vi.mocked(api.getStudySummary).mockResolvedValue(summary())
    renderSetup()

    expect(await screen.findByText('3 due')).toBeInTheDocument()
    expect(screen.getAllByText('10 new')).toHaveLength(4)
    expect(screen.getAllByRole('checkbox')).toHaveLength(4)
    screen.getAllByRole('checkbox').forEach((box) => expect(box).toBeChecked())
    // 5 due, new capped by the 20 left today
    expect(screen.getByRole('button', { name: 'Start · 25 cards' })).toBeEnabled()
  })

  it('puts the chosen topics in the session URL', async () => {
    vi.mocked(api.getStudySummary).mockResolvedValue(summary())
    const user = renderSetup()
    await screen.findByText('3 due')

    await user.click(screen.getByRole('checkbox', { name: /ASP\.NET Core/ }))
    await user.click(screen.getByRole('checkbox', { name: /System Design/ }))
    await user.click(screen.getByRole('button', { name: /Start/ }))

    expect(screen.getByTestId('location')).toHaveTextContent('/study/session?topic=CSharp&topic=Sql')
  })

  it('disables Start with "Nothing due" and the next due time when nothing is available', async () => {
    vi.mocked(api.getStudySummary).mockResolvedValue(
      summary({
        topics: summary().topics.map((t) => ({ ...t, dueCount: 0, newCount: 0 })),
        newRemainingToday: 0,
      }),
    )
    renderSetup()

    const start = await screen.findByRole('button', { name: 'Nothing due' })
    expect(start).toBeDisabled()
    expect(screen.getByText(/Next card due/)).toBeInTheDocument()
  })

  it('asks to choose a topic when none is selected', async () => {
    vi.mocked(api.getStudySummary).mockResolvedValue(summary())
    const user = renderSetup()
    await screen.findByText('3 due')

    for (const box of screen.getAllByRole('checkbox')) await user.click(box)

    expect(screen.getByRole('button', { name: 'Choose a topic' })).toBeDisabled()
  })
})

describe('selectionTotals', () => {
  it('applies the remaining daily limits once to the selection, not per topic', () => {
    const totals = selectionTotals(summary({ newRemainingToday: 12, reviewsRemainingToday: 4 }), new Set(['CSharp', 'Sql']))

    expect(totals.due).toBe(4) // 3 + 2 due, capped at 4
    expect(totals.fresh).toBe(12) // 10 + 10 new, capped at 12
  })

  it('reports the earliest next due time among the selected topics only', () => {
    const s = summary()

    expect(selectionTotals(s, new Set(['Sql', 'SystemDesign'])).nextDue).toBe('2098-01-01T00:00:00Z')
    expect(selectionTotals(s, new Set(['Sql'])).nextDue).toBe('2099-01-01T00:00:00Z')
    expect(selectionTotals(s, new Set(['CSharp'])).nextDue).toBeUndefined()
  })
})
