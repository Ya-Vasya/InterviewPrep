import { describe, expect, it } from 'vitest'
import type { StudyQueueItem } from '../../api/client'
import { currentEntry, initSession, REQUEUE_GAP, sessionReducer, type SessionState } from './sessionReducer'

const item = (n: number): StudyQueueItem => ({
  questionId: `q${n}`,
  cardId: null,
  topic: 'CSharp',
  difficulty: 'Junior',
  text: `Question ${n}`,
  answer: `Answer ${n}`,
  tags: [],
  isNew: true,
  intervals: { again: 0, hard: 1, good: 1, easy: 3 },
})

let counter = 0
const newId = () => `id-${++counter}`

const start = (count: number): SessionState => initSession(Array.from({ length: count }, (_, i) => item(i + 1)), newId)

const flipAndRate = (state: SessionState, rating: 'Again' | 'Hard' | 'Good' | 'Easy') =>
  sessionReducer(sessionReducer(state, { type: 'flip' }), {
    type: 'rate',
    rating,
    requeueKey: newId(),
    requeueReviewId: newId(),
  })

describe('sessionReducer', () => {
  it('ignores a rating until the card is flipped', () => {
    const state = start(2)

    const after = sessionReducer(state, { type: 'rate', rating: 'Good', requeueKey: 'k', requeueReviewId: 'r' })

    expect(after).toBe(state)
  })

  it('moves to the next card, unflipped, and keeps the rated card leaving', () => {
    const after = flipAndRate(start(2), 'Good')

    expect(currentEntry(after)?.item.text).toBe('Question 2')
    expect(after.flipped).toBe(false)
    expect(after.leaving?.rating).toBe('Good')
    expect(after.history).toHaveLength(1)
    expect(sessionReducer(after, { type: 'leftScreen' }).leaving).toBeNull()
  })

  it('a second rating of the same flipped card is ignored', () => {
    const flipped = sessionReducer(start(3), { type: 'flip' })
    const once = sessionReducer(flipped, { type: 'rate', rating: 'Good', requeueKey: 'k1', requeueReviewId: 'r1' })

    const twice = sessionReducer(once, { type: 'rate', rating: 'Good', requeueKey: 'k2', requeueReviewId: 'r2' })

    expect(twice).toBe(once)
  })

  it('puts an Again card back a few cards later, without intervals until the server answers', () => {
    const after = flipAndRate(start(6), 'Again')

    const copyAt = 1 + REQUEUE_GAP
    expect(after.deck).toHaveLength(7)
    expect(after.deck[copyAt].item.text).toBe('Question 1')
    expect(after.deck[copyAt].item.isNew).toBe(false)
    expect(after.deck[copyAt].intervals).toBeNull()
    expect(after.deck[copyAt].reviewId).not.toBe(after.history[0].entry.reviewId)

    const copy = after.deck[copyAt]
    const withIntervals = sessionReducer(after, {
      type: 'intervals',
      key: copy.key,
      intervals: { again: 0, hard: 1, good: 1, easy: 3 },
    })
    expect(withIntervals.deck[copyAt].intervals?.good).toBe(1)
  })

  it('puts an Again card at the end when fewer cards are left', () => {
    const after = flipAndRate(start(2), 'Again')

    expect(after.deck.map((e) => e.item.text)).toEqual(['Question 1', 'Question 2', 'Question 1'])
  })

  it('Hard, Good and Easy do not bring the card back', () => {
    for (const rating of ['Hard', 'Good', 'Easy'] as const)
      expect(flipAndRate(start(3), rating).deck).toHaveLength(3)
  })

  it('undo restores the previous card flipped, with a fresh review id', () => {
    const before = start(3)
    const original = currentEntry(before)!
    const after = sessionReducer(flipAndRate(before, 'Good'), { type: 'undo', reviewId: 'fresh' })

    expect(currentEntry(after)?.key).toBe(original.key)
    expect(currentEntry(after)?.reviewId).toBe('fresh')
    expect(after.flipped).toBe(true)
    expect(after.history).toHaveLength(0)
    expect(after.index).toBe(0)
  })

  it('undoing an Again rating removes the copy that was queued for it', () => {
    const rated = flipAndRate(start(6), 'Again')
    expect(rated.deck).toHaveLength(7)

    const undone = sessionReducer(rated, { type: 'undo', reviewId: 'fresh' })

    expect(undone.deck).toHaveLength(6)
    expect(undone.deck.map((e) => e.item.text)).toEqual(start(6).deck.map((e) => e.item.text))
  })

  it('undo steps back through several ratings, last first', () => {
    let state = start(3)
    state = flipAndRate(state, 'Good')
    state = flipAndRate(state, 'Easy')

    state = sessionReducer(state, { type: 'undo', reviewId: 'a' })
    expect(currentEntry(state)?.item.text).toBe('Question 2')
    state = sessionReducer(state, { type: 'undo', reviewId: 'b' })
    expect(currentEntry(state)?.item.text).toBe('Question 1')
    expect(sessionReducer(state, { type: 'undo', reviewId: 'c' })).toBe(state)
  })

  it('a rating that failed to save puts the card back in front of the current one', () => {
    const rated = flipAndRate(start(3), 'Again')
    const failedKey = rated.history[0].entry.key

    const restored = sessionReducer(rated, { type: 'rateFailed', key: failedKey, reviewId: 'retry' })

    expect(currentEntry(restored)?.item.text).toBe('Question 1')
    expect(currentEntry(restored)?.reviewId).toBe('retry')
    expect(restored.deck.map((e) => e.item.text)).toEqual(['Question 1', 'Question 2', 'Question 3'])
    expect(restored.history).toHaveLength(0)
  })

  it('finishes when every card is rated', () => {
    const done = flipAndRate(flipAndRate(start(2), 'Good'), 'Good')

    expect(currentEntry(done)).toBeUndefined()
    expect(sessionReducer(done, { type: 'flip' })).toBe(done)
  })
})
