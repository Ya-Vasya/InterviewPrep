import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError, type AnswerAttempt, type CheckAnswerResult, type StudyQueueItem } from '../../api/client'
import { StudySession } from './StudySession'

vi.mock('../../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/client')>()),
  api: {
    postReview: vi.fn(),
    undoReview: vi.fn(),
    getAiStatus: vi.fn(),
    checkAnswer: vi.fn(),
    getAnswerAttempts: vi.fn(),
    getNote: vi.fn(),
  },
}))

const postReview = vi.mocked(api.postReview)
const getAiStatus = vi.mocked(api.getAiStatus)
const checkAnswer = vi.mocked(api.checkAnswer)
const getAnswerAttempts = vi.mocked(api.getAnswerAttempts)
const getNote = vi.mocked(api.getNote)

const item = (n: number): StudyQueueItem => ({
  questionId: `question-${n}`,
  cardId: null,
  topic: 'Sql',
  difficulty: 'Middle',
  text: `Question ${n}?`,
  answer: `Answer ${n} with \`code\``,
  tags: [],
  source: 'Seed',
  isNew: true,
  intervals: { again: 0, hard: 1, good: 6, easy: 8 },
})

const checkResult = (overrides: Partial<CheckAnswerResult> = {}): CheckAnswerResult => ({
  attemptId: 'attempt-1',
  verdict: 'Partial',
  score: 55,
  covered: ['Singleton lifetime'],
  missing: ['Scoped vs transient'],
  feedback: 'Good start.',
  suggestedRating: 'Hard',
  ...overrides,
})

const attempt = (n: number, overrides: Partial<AnswerAttempt> = {}): AnswerAttempt => ({
  id: `old-attempt-${n}`,
  questionId: 'question-1',
  answer: `old answer ${n}`,
  verdict: 'Incorrect',
  score: 10 * n,
  covered: [],
  missing: ['everything'],
  feedback: `old feedback ${n}`,
  createdAt: '2026-10-01T09:00:00Z',
  suggestedRating: 'Again',
  ...overrides,
})

function renderSession() {
  const user = userEvent.setup()
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false }, queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <StudySession items={[item(1), item(2)]} topics={['Sql']} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return user
}

const answerBox = () => screen.getByRole('textbox', { name: /your answer/i })
const checkButton = () => screen.getByRole('button', { name: /check with ai/i })

beforeEach(() => {
  vi.clearAllMocks()
  getAiStatus.mockResolvedValue({ enabled: true, usedToday: 3, dailyCap: 50 })
  getAnswerAttempts.mockResolvedValue({ items: [], total: 0, limit: 50, offset: 0 })
  getNote.mockResolvedValue(null)
  postReview.mockImplementation(async ({ reviewId }) => ({
    reviewId,
    cardId: 'card',
    dueAt: '2026-10-12T09:00:00Z',
    intervalDays: 1,
    nextIntervals: { again: 0, hard: 1, good: 1, easy: 3 },
  }))
})

describe('checking an answer with AI', () => {
  it('shows no AI controls when AI is not configured', async () => {
    getAiStatus.mockResolvedValue({ enabled: false, usedToday: 0, dailyCap: 50 })
    renderSession()

    await waitFor(() => expect(getAiStatus).toHaveBeenCalled())

    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /check with ai/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/AI calls today/i)).not.toBeInTheDocument()
  })

  it('offers an optional answer box and the usage counter next to it', async () => {
    renderSession()

    expect(await screen.findByRole('textbox', { name: /your answer/i })).toBeInTheDocument()
    expect(screen.getByText('AI calls today: 3 / 50')).toBeInTheDocument()
  })

  it('cannot check an empty answer or one over 4,000 characters', async () => {
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })

    expect(checkButton()).toBeDisabled()

    await user.type(answerBox(), '   ')
    expect(checkButton()).toBeDisabled()

    await user.type(answerBox(), 'real answer')
    expect(checkButton()).toBeEnabled()

    expect(answerBox()).toHaveAttribute('maxlength', '4000')
  })

  it('does not flip the card or rate it while typing in the answer box', async () => {
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })

    await user.click(answerBox())
    await user.keyboard('use 1 and 2 and space here')

    expect(answerBox()).toHaveValue('use 1 and 2 and space here')
    expect(screen.queryByText(/Answer 1/)).not.toBeInTheDocument()
    expect(postReview).not.toHaveBeenCalled()
  })

  it('sends the answer, then flips the card and shows the verdict next to the reference answer', async () => {
    checkAnswer.mockResolvedValue(checkResult())
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })

    await user.type(answerBox(), 'Singletons live for the whole app.')
    await user.click(checkButton())

    expect(checkAnswer).toHaveBeenCalledWith('question-1', 'Singletons live for the whole app.')
    expect(await screen.findByText(/Answer 1/)).toBeInTheDocument()
    const result = screen.getByRole('region', { name: /ai verdict/i })
    expect(within(result).getByText(/partial/i)).toBeInTheDocument()
    expect(within(result).getByText(/55/)).toBeInTheDocument()
    expect(within(result).getByText('Singleton lifetime')).toBeInTheDocument()
    expect(within(result).getByText('Scoped vs transient')).toBeInTheDocument()
    expect(within(result).getByText('Good start.')).toBeInTheDocument()
  })

  it('highlights the suggested rating but never applies it', async () => {
    checkAnswer.mockResolvedValue(checkResult({ verdict: 'Partial', suggestedRating: 'Hard' }))
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })

    await user.type(answerBox(), 'something')
    await user.click(checkButton())

    expect(await screen.findByRole('button', { name: /^Hard.*suggested/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Good/ })).not.toHaveAccessibleName(/suggested/i)
    expect(postReview).not.toHaveBeenCalled()
  })

  it('sends the attempt id with the rating, so an accepted suggestion can be recorded as AI-assisted', async () => {
    checkAnswer.mockResolvedValue(checkResult({ attemptId: 'attempt-42', suggestedRating: 'Hard' }))
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })
    await user.type(answerBox(), 'something')
    await user.click(checkButton())

    await user.click(await screen.findByRole('button', { name: /^Hard/ }))

    await waitFor(() => expect(postReview).toHaveBeenCalled())
    expect(postReview.mock.calls[0][0]).toMatchObject({ questionId: 'question-1', rating: 'Hard', attemptId: 'attempt-42' })
  })

  it('leaves the attempt id out when the card was not checked', async () => {
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })

    await user.click(screen.getByRole('button', { name: /Question 1\?/ }))
    await user.click(screen.getByRole('button', { name: /^Good/ }))

    await waitFor(() => expect(postReview).toHaveBeenCalled())
    expect(postReview.mock.calls[0][0].attemptId).toBeUndefined()
  })

  it('does not carry a check over to the next card', async () => {
    checkAnswer.mockResolvedValue(checkResult({ attemptId: 'attempt-1' }))
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })
    await user.type(answerBox(), 'something')
    await user.click(checkButton())
    await user.click(await screen.findByRole('button', { name: /^Hard/ }))

    // The next card starts with an empty box and no verdict.
    expect(await screen.findByText('Question 2?')).toBeInTheDocument()
    expect(answerBox()).toHaveValue('')
    expect(screen.queryByRole('region', { name: /ai verdict/i })).not.toBeInTheDocument()
  })

  it('ignores a check that comes back after the user has moved on to another card', async () => {
    let resolveCheck!: (result: CheckAnswerResult) => void
    checkAnswer.mockReturnValue(new Promise<CheckAnswerResult>((resolve) => (resolveCheck = resolve)))
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })
    await user.type(answerBox(), 'something')
    await user.click(checkButton())

    // While the AI is still thinking, the user flips the card by hand, rates it and reaches the next one.
    await user.click(screen.getByRole('button', { name: /Question 1\?/ }))
    await user.click(screen.getByRole('button', { name: /^Good/ }))
    expect(await screen.findByText('Question 2?')).toBeInTheDocument()

    await act(async () => resolveCheck(checkResult()))

    expect(screen.queryByText(/Answer 2/)).not.toBeInTheDocument() // card 2 stays face down
    expect(screen.queryByRole('region', { name: /ai verdict/i })).not.toBeInTheDocument()
  })

  it('shows why a check failed and leaves the card unflipped', async () => {
    checkAnswer.mockRejectedValue(new ApiError(429, 'The daily limit of 50 AI calls is used up.'))
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })

    await user.type(answerBox(), 'something')
    await user.click(checkButton())

    expect(await screen.findByRole('alert')).toHaveTextContent('The daily limit of 50 AI calls is used up.')
    expect(screen.queryByText(/Answer 1/)).not.toBeInTheDocument()
    expect(answerBox()).toHaveValue('something') // the typed answer is not lost
  })

  it('lists earlier attempts on the back of the card', async () => {
    getAnswerAttempts.mockResolvedValue({ items: [attempt(2), attempt(1)], total: 2, limit: 50, offset: 0 })
    const user = renderSession()
    await screen.findByRole('textbox', { name: /your answer/i })

    await user.click(screen.getByRole('button', { name: /Question 1\?/ }))

    const history = await screen.findByText(/previous attempts \(2\)/i)
    await user.click(history)
    expect(getAnswerAttempts).toHaveBeenCalledWith('question-1')
    expect(screen.getByText('old answer 2')).toBeInTheDocument()
    expect(screen.getByText('old answer 1')).toBeInTheDocument()
  })
})
