import type { Draft } from '../../api/client'

/** A Draft the user has not decided on yet is `pending`; once saved as a question it is `accepted`. Discarded Drafts are removed. */
export type DraftStatus = 'pending' | 'accepted'

export interface DraftItem {
  /** Client-only id; Drafts have no identity on the server. */
  id: string
  draft: Draft
  status: DraftStatus
  /** Why the server refused to accept it, keyed by field. Cleared when the Draft is edited. */
  errors: Record<string, string[]> | null
}

export type RunState =
  | { phase: 'idle' }
  /** The single provider call is in flight; the panel shows `count` placeholders. */
  | { phase: 'running'; count: number }
  | { phase: 'failed'; message: string }

export interface GenerateState {
  run: RunState
  items: DraftItem[]
  selectedId: string | null
  duplicatesDropped: number
}

export type GenerateAction =
  | { type: 'start'; count: number }
  | { type: 'succeeded'; drafts: Draft[]; ids: string[]; duplicatesDropped: number }
  | { type: 'failed'; message: string }
  | { type: 'select'; id: string }
  | { type: 'edit'; id: string; patch: Partial<Draft> }
  | { type: 'discard'; id: string }
  | { type: 'accepted'; ids: string[] }
  | { type: 'rejected'; errors: Record<string, Record<string, string[]>> }

export const initialGenerateState: GenerateState = {
  run: { phase: 'idle' },
  items: [],
  selectedId: null,
  duplicatesDropped: 0,
}

export const pendingCount = (state: GenerateState) => state.items.filter((i) => i.status === 'pending').length

/** Drafts that would be lost on leaving the page: the pending ones, and those a running call is about to deliver. */
export const hasUnsavedDrafts = (state: GenerateState) => state.run.phase === 'running' || pendingCount(state) > 0

export function generateReducer(state: GenerateState, action: GenerateAction): GenerateState {
  switch (action.type) {
    case 'start':
      // A new run replaces the previous list.
      return { run: { phase: 'running', count: action.count }, items: [], selectedId: null, duplicatesDropped: 0 }

    case 'succeeded': {
      const items = action.drafts.map((draft, i): DraftItem => ({
        id: action.ids[i],
        draft,
        status: 'pending',
        errors: null,
      }))
      return {
        run: { phase: 'idle' },
        items,
        selectedId: items[0]?.id ?? null,
        duplicatesDropped: action.duplicatesDropped,
      }
    }

    case 'failed':
      return { ...state, run: { phase: 'failed', message: action.message } }

    case 'select':
      return { ...state, selectedId: action.id }

    case 'edit':
      return {
        ...state,
        items: state.items.map((i) =>
          i.id === action.id && i.status === 'pending'
            ? { ...i, draft: { ...i.draft, ...action.patch }, errors: null }
            : i,
        ),
      }

    case 'discard': {
      const index = state.items.findIndex((i) => i.id === action.id)
      if (index < 0 || state.items[index].status !== 'pending') return state

      const items = state.items.filter((i) => i.id !== action.id)
      // The selection moves to the neighbour, so reviewing can go on without a click.
      const selectedId =
        state.selectedId === action.id ? (items[index] ?? items[index - 1])?.id ?? null : state.selectedId
      return { ...state, items, selectedId }
    }

    case 'accepted':
      return {
        ...state,
        items: state.items.map((i) => (action.ids.includes(i.id) ? { ...i, status: 'accepted', errors: null } : i)),
      }

    case 'rejected':
      return {
        ...state,
        items: state.items.map((i) => (action.errors[i.id] ? { ...i, errors: action.errors[i.id] } : i)),
      }
  }
}
