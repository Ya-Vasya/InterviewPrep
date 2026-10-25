import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  api,
  ApiError,
  type AnswerAttempt,
  type CheckAnswerResult,
  type Note,
  type StudyQueueItem,
} from '../../api/client'
import { StudySession } from './StudySession'

vi.mock('../../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/client')>()),
  api: {
    postReview: vi.fn(),
    undoReview: vi.fn(),
    getAiStatus: vi.fn(),
    checkAnswer: vi.fn(),
    getAnswerAttempts: vi.fn(),
    explainMistakes: vi.fn(),
    getNote: vi.fn(),
    saveNote: vi.fn(),
  },
}))

const getAiStatus = vi.mocked(api.getAiStatus)
const checkAnswer = vi.mocked(api.checkAnswer)
const getAnswerAttempts = vi.mocked(api.getAnswerAttempts)
const explainMistakes = vi.mocked(api.explainMistakes)
const getNote = vi.mocked(api.getNote)
const saveNote = vi.mocked(api.saveNote)

const EXPLANATION = '### Scoped vs transient\n\nA scoped service lives per request.\n\n```csharp\npublic class Foo { }\n```'

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

const note = (text: string): Note => ({ questionId: 'question-1', text, updatedAt: '2026-10-02T09:00:00Z' })

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

/** Types an answer, checks it and waits for the flipped card with the verdict panel. */
async function checkAnAnswer(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByRole('textbox', { name: /your answer/i }), 'my answer')
  await user.click(screen.getByRole('button', { name: /check with ai/i }))
  return screen.findByRole('region', { name: /ai verdict/i })
}

const explainButton = (scope: ReturnType<typeof within> | typeof screen = screen) =>
  scope.getByRole('button', { name: /explain my mistakes/i })

beforeEach(() => {
  vi.clearAllMocks()
  getAiStatus.mockResolvedValue({ enabled: true, usedToday: 3, dailyCap: 50 })
  getAnswerAttempts.mockResolvedValue({ items: [], total: 0, limit: 50, offset: 0 })
  getNote.mockResolvedValue(null)
  checkAnswer.mockResolvedValue(checkResult())
  explainMistakes.mockResolvedValue({ attemptId: 'attempt-1', explanation: EXPLANATION })
  saveNote.mockImplementation(async (questionId, text) => ({ questionId, text, updatedAt: '2026-10-14T09:00:00Z' }))
})

describe('explaining mistakes', () => {
  it.each(['Partial', 'Incorrect'] as const)('is offered on a %s check result, with the usage counter beside it', async (verdict) => {
    checkAnswer.mockResolvedValue(checkResult({ verdict }))
    const user = renderSession()

    const result = await checkAnAnswer(user)

    expect(explainButton(within(result))).toBeInTheDocument()
    expect(within(result).getByText('AI calls today: 3 / 50')).toBeInTheDocument()
  })

  it('is not offered on a correct check result', async () => {
    checkAnswer.mockResolvedValue(checkResult({ verdict: 'Correct', suggestedRating: 'Good' }))
    const user = renderSession()

    await checkAnAnswer(user)

    expect(screen.queryByRole('button', { name: /explain my mistakes/i })).not.toBeInTheDocument()
  })

  it('is hidden when AI is not configured', async () => {
    getAiStatus.mockResolvedValue({ enabled: false, usedToday: 0, dailyCap: 50 })
    getAnswerAttempts.mockResolvedValue({ items: [attempt(1)], total: 1, limit: 50, offset: 0 })
    const user = renderSession()
    await waitFor(() => expect(getAiStatus).toHaveBeenCalled())

    await user.click(await screen.findByRole('button', { name: /Question 1\?/ }))

    expect(screen.queryByRole('button', { name: /explain my mistakes/i })).not.toBeInTheDocument()
  })

  it('asks for the explanation of that attempt and renders it as markdown with highlighted code', async () => {
    const user = renderSession()
    const result = await checkAnAnswer(user)

    await user.click(explainButton(within(result)))

    expect(explainMistakes).toHaveBeenCalledWith('attempt-1')
    const explanation = await screen.findByRole('region', { name: /explanation/i })
    expect(within(explanation).getByRole('heading', { name: 'Scoped vs transient' })).toBeInTheDocument()
    expect(within(explanation).getByText(/lives per request/)).toBeInTheDocument()
    expect(explanation.querySelector("pre code .hljs-keyword")).not.toBeNull()
  })

  it('shows why an explanation failed and lets the user try again', async () => {
    explainMistakes.mockRejectedValueOnce(new ApiError(503, 'The AI provider did not answer in time. Try again.'))
    const user = renderSession()
    const result = await checkAnAnswer(user)

    await user.click(explainButton(within(result)))

    expect(await within(result).findByRole('alert')).toHaveTextContent('did not answer in time')
    expect(explainButton(within(result))).toBeEnabled()
  })

  it('is offered on past partial and incorrect attempts, not on correct ones', async () => {
    getAnswerAttempts.mockResolvedValue({
      items: [attempt(3, { verdict: 'Correct' }), attempt(2, { verdict: 'Partial' }), attempt(1)],
      total: 3,
      limit: 50,
      offset: 0,
    })
    const user = renderSession()
    await user.click(await screen.findByRole('button', { name: /Question 1\?/ }))
    await user.click(await screen.findByText(/previous attempts \(3\)/i))

    const rows = screen.getAllByRole('listitem')
    expect(within(rows[0]).queryByRole('button', { name: /explain my mistakes/i })).not.toBeInTheDocument()
    expect(within(rows[1]).getByRole('button', { name: /explain my mistakes/i })).toBeInTheDocument()
    expect(within(rows[2]).getByRole('button', { name: /explain my mistakes/i })).toBeInTheDocument()
  })

  it('explains a past attempt on demand', async () => {
    getAnswerAttempts.mockResolvedValue({ items: [attempt(1)], total: 1, limit: 50, offset: 0 })
    explainMistakes.mockResolvedValue({ attemptId: 'old-attempt-1', explanation: EXPLANATION })
    const user = renderSession()
    await user.click(await screen.findByRole('button', { name: /Question 1\?/ }))
    await user.click(await screen.findByText(/previous attempts \(1\)/i))

    await user.click(within(screen.getByRole('listitem')).getByRole('button', { name: /explain my mistakes/i }))

    expect(explainMistakes).toHaveBeenCalledWith('old-attempt-1')
    expect(await screen.findByRole('region', { name: /explanation/i })).toBeInTheDocument()
  })
})

describe('notes', () => {
  async function explain(user: ReturnType<typeof userEvent.setup>) {
    const result = await checkAnAnswer(user)
    await user.click(explainButton(within(result)))
    return screen.findByRole('region', { name: /explanation/i })
  }

  it('saves the explanation as the note when the question has none, without asking', async () => {
    const user = renderSession()
    const explanation = await explain(user)

    await user.click(within(explanation).getByRole('button', { name: /save as note/i }))

    await waitFor(() => expect(saveNote).toHaveBeenCalledWith('question-1', EXPLANATION))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    // The note now shows on the card.
    const shown = await screen.findByRole('region', { name: /your note/i })
    expect(within(shown).getByRole('heading', { name: 'Scoped vs transient' })).toBeInTheDocument()
  })

  it('asks before replacing an existing note, and keeps it on Cancel', async () => {
    getNote.mockResolvedValue(note('My old note'))
    const user = renderSession()
    const explanation = await explain(user)

    await user.click(within(explanation).getByRole('button', { name: /save as note/i }))

    const confirm = await screen.findByRole('alertdialog')
    expect(within(confirm).getByText(/already have a note/i)).toBeInTheDocument()
    expect(saveNote).not.toHaveBeenCalled()

    await user.click(within(confirm).getByRole('button', { name: /cancel/i }))

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(saveNote).not.toHaveBeenCalled()
    expect(within(screen.getByRole('region', { name: /your note/i })).getByText('My old note')).toBeInTheDocument()
  })

  it('replaces the existing note after the user confirms', async () => {
    getNote.mockResolvedValue(note('My old note'))
    const user = renderSession()
    const explanation = await explain(user)

    await user.click(within(explanation).getByRole('button', { name: /save as note/i }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: /replace/i }))

    await waitFor(() => expect(saveNote).toHaveBeenCalledWith('question-1', EXPLANATION))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    const shown = await screen.findByRole('region', { name: /your note/i })
    expect(within(shown).queryByText('My old note')).not.toBeInTheDocument()
    expect(within(shown).getByRole('heading', { name: 'Scoped vs transient' })).toBeInTheDocument()
  })

  it('checks for an existing note at click time, so a note saved elsewhere is not overwritten silently', async () => {
    const user = renderSession()
    const explanation = await explain(user)
    getNote.mockResolvedValue(note('Saved from another tab')) // appeared after the card was shown

    await user.click(within(explanation).getByRole('button', { name: /save as note/i }))

    expect(await screen.findByRole('alertdialog')).toBeInTheDocument()
    expect(saveNote).not.toHaveBeenCalled()
  })

  it('shows why saving failed', async () => {
    saveNote.mockRejectedValue(new ApiError(500, 'Boom'))
    const user = renderSession()
    const explanation = await explain(user)

    await user.click(within(explanation).getByRole('button', { name: /save as note/i }))

    expect(await within(explanation).findByRole('alert')).toHaveTextContent('Boom')
  })

  it('shows the note on the back of the card only, even when AI is not configured', async () => {
    getAiStatus.mockResolvedValue({ enabled: false, usedToday: 0, dailyCap: 50 })
    getNote.mockResolvedValue(note('Remember: scoped is per request'))
    const user = renderSession()

    expect(screen.queryByText(/scoped is per request/)).not.toBeInTheDocument() // front of the card

    await user.click(await screen.findByRole('button', { name: /Question 1\?/ }))

    const shown = await screen.findByRole('region', { name: /your note/i })
    expect(within(shown).getByText('Remember: scoped is per request')).toBeInTheDocument()
    expect(getNote).toHaveBeenCalledWith('question-1')
  })

  it('shows no note section when the question has no note', async () => {
    const user = renderSession()

    await user.click(await screen.findByRole('button', { name: /Question 1\?/ }))
    await screen.findByText(/Answer 1/)

    expect(screen.queryByRole('region', { name: /your note/i })).not.toBeInTheDocument()
  })
})
