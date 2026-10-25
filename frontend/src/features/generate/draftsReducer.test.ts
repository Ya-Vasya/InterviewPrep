import { describe, expect, it } from 'vitest'
import type { Draft } from '../../api/client'
import {
  generateReducer,
  hasUnsavedDrafts,
  initialGenerateState,
  pendingCount,
  type GenerateAction,
  type GenerateState,
} from './draftsReducer'

const draft = (n: number): Draft => ({
  topic: 'CSharp',
  difficulty: 'Middle',
  text: `Question ${n}?`,
  answer: `Answer ${n}`,
  tags: ['async'],
})

const run = (state: GenerateState, ...actions: GenerateAction[]) => actions.reduce(generateReducer, state)

const withDrafts = (count = 3) =>
  run(initialGenerateState, {
    type: 'succeeded',
    drafts: Array.from({ length: count }, (_, i) => draft(i + 1)),
    ids: Array.from({ length: count }, (_, i) => `id-${i + 1}`),
    duplicatesDropped: 0,
  })

describe('generateReducer', () => {
  it('shows placeholders while running and counts it as unsaved', () => {
    const state = run(initialGenerateState, { type: 'start', count: 4 })

    expect(state.run).toEqual({ phase: 'running', count: 4 })
    expect(hasUnsavedDrafts(state)).toBe(true)
  })

  it('replaces the previous list when a new run starts', () => {
    const state = run(withDrafts(), { type: 'start', count: 2 })

    expect(state.items).toEqual([])
    expect(state.selectedId).toBeNull()
  })

  it('lists the Drafts as pending, selects the first and remembers the skipped duplicates', () => {
    const state = run(initialGenerateState, {
      type: 'succeeded', drafts: [draft(1), draft(2)], ids: ['a', 'b'], duplicatesDropped: 2,
    })

    expect(state.items.map((i) => [i.id, i.status])).toEqual([['a', 'pending'], ['b', 'pending']])
    expect(state.selectedId).toBe('a')
    expect(state.duplicatesDropped).toBe(2)
    expect(state.run).toEqual({ phase: 'idle' })
  })

  it('has nothing selected, and nothing to lose, when every Draft was a duplicate', () => {
    const state = run(initialGenerateState, { type: 'succeeded', drafts: [], ids: [], duplicatesDropped: 3 })

    expect(state.selectedId).toBeNull()
    expect(hasUnsavedDrafts(state)).toBe(false)
  })

  it('keeps the failure message and no Drafts', () => {
    const state = run(initialGenerateState, { type: 'start', count: 2 }, { type: 'failed', message: 'Daily limit reached' })

    expect(state.run).toEqual({ phase: 'failed', message: 'Daily limit reached' })
    expect(hasUnsavedDrafts(state)).toBe(false)
  })

  it('edits a pending Draft and clears the errors the server gave it', () => {
    const state = run(
      withDrafts(),
      { type: 'rejected', errors: { 'id-2': { text: ['bad'] } } },
      { type: 'edit', id: 'id-2', patch: { text: 'Fixed?' } },
    )

    const edited = state.items[1]
    expect(edited.draft.text).toBe('Fixed?')
    expect(edited.draft.answer).toBe('Answer 2')
    expect(edited.errors).toBeNull()
  })

  it('does not edit an accepted Draft', () => {
    const state = run(withDrafts(), { type: 'accepted', ids: ['id-1'] }, { type: 'edit', id: 'id-1', patch: { text: 'Changed' } })

    expect(state.items[0].draft.text).toBe('Question 1?')
  })

  it('marks accepted Drafts and leaves the rest pending', () => {
    const state = run(withDrafts(), { type: 'accepted', ids: ['id-1', 'id-3'] })

    expect(state.items.map((i) => i.status)).toEqual(['accepted', 'pending', 'accepted'])
    expect(pendingCount(state)).toBe(1)
  })

  it('records why a Draft was rejected, and keeps it pending', () => {
    const state = run(withDrafts(), { type: 'rejected', errors: { 'id-2': { text: ['already exists'] } } })

    expect(state.items[1]).toMatchObject({ status: 'pending', errors: { text: ['already exists'] } })
    expect(state.items[0].errors).toBeNull()
  })

  it('discards a Draft and moves the selection to its neighbour', () => {
    const middle = run(withDrafts(), { type: 'select', id: 'id-2' }, { type: 'discard', id: 'id-2' })
    expect(middle.items.map((i) => i.id)).toEqual(['id-1', 'id-3'])
    expect(middle.selectedId).toBe('id-3')

    const last = run(withDrafts(), { type: 'select', id: 'id-3' }, { type: 'discard', id: 'id-3' })
    expect(last.selectedId).toBe('id-2')
  })

  it('keeps the selection when another Draft is discarded', () => {
    const state = run(withDrafts(), { type: 'discard', id: 'id-3' })

    expect(state.selectedId).toBe('id-1')
  })

  it('has nothing selected once the last Draft is discarded', () => {
    const state = run(withDrafts(1), { type: 'discard', id: 'id-1' })

    expect(state.selectedId).toBeNull()
    expect(hasUnsavedDrafts(state)).toBe(false)
  })

  it('cannot discard an accepted Draft', () => {
    const state = run(withDrafts(1), { type: 'accepted', ids: ['id-1'] }, { type: 'discard', id: 'id-1' })

    expect(state.items).toHaveLength(1)
  })

  it('has nothing to lose once every Draft is accepted', () => {
    const state = run(withDrafts(2), { type: 'accepted', ids: ['id-1', 'id-2'] })

    expect(hasUnsavedDrafts(state)).toBe(false)
  })
})
