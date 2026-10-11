import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, type StudyQueueItem } from '../../api/client'
import { StudySession } from './StudySession'

vi.mock('../../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/client')>()),
  api: { postReview: vi.fn(), undoReview: vi.fn() },
}))

const postReview = vi.mocked(api.postReview)
const undoReview = vi.mocked(api.undoReview)

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/

const item = (n: number): StudyQueueItem => ({
  questionId: `question-${n}`,
  cardId: null,
  topic: 'Sql',
  difficulty: 'Middle',
  text: `Question ${n}?`,
  answer: `Answer ${n} with \`code\``,
  tags: [],
  isNew: true,
  intervals: { again: 0, hard: 1, good: 6, easy: 8 },
})

function renderSession(count = 3) {
  const user = userEvent.setup()
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <StudySession items={Array.from({ length: count }, (_, i) => item(i + 1))} topics={['Sql']} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return user
}

const ratingButton = (name: RegExp) => screen.queryByRole('button', { name })

beforeEach(() => {
  vi.clearAllMocks()
  postReview.mockImplementation(async ({ reviewId }) => ({
    reviewId,
    cardId: 'card',
    dueAt: '2026-10-12T09:00:00Z',
    intervalDays: 1,
    nextIntervals: { again: 0, hard: 1, good: 1, easy: 3 },
  }))
  undoReview.mockResolvedValue(undefined)
})

describe('StudySession', () => {
  it('shows only the question, and no rating buttons, until the card is flipped', () => {
    renderSession()

    expect(screen.getByText('Question 1?')).toBeInTheDocument()
    expect(screen.queryByText(/Answer 1/)).not.toBeInTheDocument()
    expect(ratingButton(/^Again/)).not.toBeInTheDocument()
    expect(ratingButton(/^Good/)).not.toBeInTheDocument()
  })

  it('flips on tap and then offers the four ratings with their intervals', async () => {
    const user = renderSession()

    await user.click(screen.getByRole('button', { name: /Question 1\?/ }))

    expect(screen.getByText(/Answer 1/)).toBeInTheDocument()
    expect(ratingButton(/^Again · now/)).toBeInTheDocument()
    expect(ratingButton(/^Hard · 1 d/)).toBeInTheDocument()
    expect(ratingButton(/^Good · 6 d/)).toBeInTheDocument()
    expect(ratingButton(/^Easy · 8 d/)).toBeInTheDocument()
  })

  it('Space flips the card', async () => {
    const user = renderSession()

    await user.keyboard(' ')

    expect(screen.getByText(/Answer 1/)).toBeInTheDocument()
  })

  it('ignores the number keys until the card is flipped', async () => {
    const user = renderSession()

    await user.keyboard('3')

    expect(postReview).not.toHaveBeenCalled()
    expect(screen.getByText('Question 1?')).toBeInTheDocument()
  })

  it('1-4 rate the flipped card with a GUID review id and bring in the next card', async () => {
    const user = renderSession()

    await user.keyboard(' ')
    await user.keyboard('3')

    await waitFor(() => expect(postReview).toHaveBeenCalledTimes(1))
    expect(postReview).toHaveBeenCalledWith({
      questionId: 'question-1',
      rating: 'Good',
      reviewId: expect.stringMatching(GUID),
    })
    expect(screen.getByText('Card 2 of 3')).toBeInTheDocument()
    expect(ratingButton(/^Good/)).not.toBeInTheDocument() // the next card starts unflipped
  })

  it.each([
    ['1', 'Again'],
    ['2', 'Hard'],
    ['3', 'Good'],
    ['4', 'Easy'],
  ])('key %s sends the %s rating', async (key, rating) => {
    const user = renderSession()

    await user.keyboard(' ')
    await user.keyboard(key)

    await waitFor(() => expect(postReview).toHaveBeenCalledWith(expect.objectContaining({ rating })))
  })

  it('records one review when a rating is tapped twice in a row', async () => {
    const user = renderSession()
    await user.keyboard(' ')

    await user.keyboard('33')

    await waitFor(() => expect(postReview).toHaveBeenCalledTimes(1))
    expect(screen.getByText('Card 2 of 3')).toBeInTheDocument()
  })

  it('brings an Again card back later in the session with the intervals the server returned', async () => {
    postReview.mockResolvedValueOnce({
      reviewId: 'x',
      cardId: 'card',
      dueAt: '2026-10-11T09:00:00Z',
      intervalDays: 0,
      nextIntervals: { again: 0, hard: 1, good: 1, easy: 3 },
    })
    const user = renderSession(2)

    await user.keyboard(' ')
    await user.keyboard('1')
    await waitFor(() => expect(screen.getByText('Card 2 of 3')).toBeInTheDocument())

    await user.keyboard(' 3') // Question 2, then Good
    await user.keyboard(' ')

    expect(screen.getByText('Question 1?')).toBeInTheDocument() // back again
    expect(ratingButton(/^Good · 1 d/)).toBeInTheDocument() // restarted intervals, not 6 d
    expect(postReview.mock.calls[0][0].reviewId).not.toBe(postReview.mock.calls[2]?.[0]?.reviewId)
  })

  it('Undo deletes the last review and shows that card again', async () => {
    const user = renderSession()
    await user.keyboard(' ')
    await user.keyboard('4')
    await waitFor(() => expect(postReview).toHaveBeenCalled())
    const { reviewId } = postReview.mock.calls[0][0]

    await user.click(screen.getByRole('button', { name: /Undo/ }))

    await waitFor(() => expect(undoReview).toHaveBeenCalledWith(reviewId))
    expect(await screen.findByText('Card 1 of 3')).toBeInTheDocument()
    expect(screen.getByText(/Answer 1/)).toBeInTheDocument()
  })

  it('Undo is disabled before anything has been rated', () => {
    renderSession()

    expect(screen.getByRole('button', { name: /Undo/ })).toBeDisabled()
  })

  it('shows a completion message after the last card', async () => {
    const user = renderSession(1)

    await user.keyboard(' ')
    await user.keyboard('3')

    expect(await screen.findByText('Session complete')).toBeInTheDocument()
  })

  it('shows "Nothing due" for an empty queue', () => {
    renderSession(0)

    expect(screen.getByRole('heading', { name: 'Nothing due' })).toBeInTheDocument()
  })

  it('puts the card back and explains when a rating cannot be saved', async () => {
    postReview.mockRejectedValueOnce(new Error('offline'))
    const user = renderSession()

    await user.keyboard(' ')
    await user.keyboard('3')

    expect(await screen.findByRole('alert')).toHaveTextContent(/Couldn't save/)
    expect(screen.getByText('Question 1?')).toBeInTheDocument()
  })
})
