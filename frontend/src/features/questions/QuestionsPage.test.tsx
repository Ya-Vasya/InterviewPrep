import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, type Page, type Question } from '../../api/client'
import { QuestionsPage } from './QuestionsPage'

vi.mock('../../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/client')>()),
  api: { getTopics: vi.fn(), getQuestions: vi.fn() },
}))

const question = (n: number): Question => ({
  id: `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`,
  topic: 'CSharp',
  text: `Question ${n}`,
  answer: 'Answer',
  difficulty: 'Junior',
  tags: [],
  source: 'Seed',
})

const page = (from: number, count: number, total: number): Page<Question> => ({
  items: Array.from({ length: count }, (_, i) => question(from + i)),
  total,
  limit: 2,
  offset: from - 1,
})

function renderPage() {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <QuestionsPage />
    </QueryClientProvider>,
  )
  return userEvent.setup()
}

beforeEach(() => {
  vi.mocked(api.getTopics).mockReset().mockResolvedValue([
    { topic: 'CSharp', name: 'C#', questionCount: 3 },
  ])
  vi.mocked(api.getQuestions).mockReset()
})

describe('QuestionsPage', () => {
  it('shows the first page and how many questions match in total', async () => {
    vi.mocked(api.getQuestions).mockResolvedValue(page(1, 2, 3))
    renderPage()

    expect(await screen.findByText('Question 1')).toBeInTheDocument()
    expect(screen.getByText('Question 2')).toBeInTheDocument()
    expect(screen.getByText('Showing 2 of 3')).toBeInTheDocument()
  })

  it('loads the next page from the end of the shown list and hides the button when done', async () => {
    vi.mocked(api.getQuestions).mockResolvedValueOnce(page(1, 2, 3)).mockResolvedValueOnce(page(3, 1, 3))
    const user = renderPage()

    await user.click(await screen.findByRole('button', { name: 'Load more' }))

    expect(await screen.findByText('Question 3')).toBeInTheDocument()
    expect(api.getQuestions).toHaveBeenLastCalledWith({ topic: undefined, offset: 2 })
    expect(screen.getByText('Showing 3 of 3')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('has no Load more button when everything fits on one page', async () => {
    vi.mocked(api.getQuestions).mockResolvedValue(page(1, 2, 2))
    renderPage()

    expect(await screen.findByText('Showing 2 of 2')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('starts again from the first page when the topic changes', async () => {
    vi.mocked(api.getQuestions).mockResolvedValue(page(1, 2, 3))
    const user = renderPage()
    await screen.findByText('Question 1')

    await user.selectOptions(screen.getByRole('combobox'), 'CSharp')

    expect(api.getQuestions).toHaveBeenLastCalledWith({ topic: 'CSharp', offset: 0 })
  })
})
