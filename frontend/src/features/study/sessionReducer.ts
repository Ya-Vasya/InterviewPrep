import type { Rating, RatingIntervals, StudyQueueItem } from '../../api/client'

/** An Again card comes back after this many other cards (or at the end, if fewer are left). */
export const REQUEUE_GAP = 3

/** One showing of a card. The same question can appear twice in a session when it was rated Again. */
export interface Entry {
  key: string
  /** Idempotency key sent with the rating; regenerated whenever the card is shown again. */
  reviewId: string
  item: StudyQueueItem
  /** Null for a re-queued card until the server has returned its new intervals. */
  intervals: RatingIntervals | null
}

interface HistoryRecord {
  entry: Entry
  rating: Rating
  /** Key of the copy that rating Again put back into the deck, if any. */
  requeuedKey: string | null
}

export interface SessionState {
  deck: Entry[]
  /** Position of the current card; equal to deck.length when the session is finished. */
  index: number
  flipped: boolean
  history: HistoryRecord[]
  /** The card that was just rated and is animating off screen. */
  leaving: { entry: Entry; rating: Rating } | null
}

export type SessionAction =
  | { type: 'flip' }
  | { type: 'rate'; rating: Rating; requeueKey: string; requeueReviewId: string }
  | { type: 'undo'; reviewId: string }
  | { type: 'rateFailed'; key: string; reviewId: string }
  | { type: 'intervals'; key: string; intervals: RatingIntervals }
  | { type: 'leftScreen' }

export function initSession(items: StudyQueueItem[], newId: () => string): SessionState {
  return {
    deck: items.map((item) => ({ key: newId(), reviewId: newId(), item, intervals: item.intervals })),
    index: 0,
    flipped: false,
    history: [],
    leaving: null,
  }
}

export function sessionReducer(state: SessionState, action: SessionAction): SessionState {
  switch (action.type) {
    case 'flip':
      return state.index < state.deck.length ? { ...state, flipped: true } : state

    case 'rate': {
      const entry = state.deck[state.index]
      // Ratings only exist on a flipped card, so a double tap cannot rate twice.
      if (!entry || !state.flipped) return state

      const deck = [...state.deck]
      let requeuedKey: string | null = null
      if (action.rating === 'Again') {
        const at = Math.min(deck.length, state.index + 1 + REQUEUE_GAP)
        deck.splice(at, 0, {
          key: action.requeueKey,
          reviewId: action.requeueReviewId,
          item: { ...entry.item, isNew: false },
          intervals: null,
        })
        requeuedKey = action.requeueKey
      }

      return {
        deck,
        index: state.index + 1,
        flipped: false,
        history: [...state.history, { entry, rating: action.rating, requeuedKey }],
        leaving: { entry, rating: action.rating },
      }
    }

    case 'undo': {
      const record = state.history.at(-1)
      if (!record) return state

      const deck = state.deck.filter((e) => e.key !== record.requeuedKey)
      const index = state.index - 1
      deck[index] = { ...record.entry, reviewId: action.reviewId }

      // The restored card has been seen, so it comes back flipped, ready to be rated again.
      return { deck, index, flipped: true, history: state.history.slice(0, -1), leaving: null }
    }

    case 'rateFailed': {
      const record = state.history.find((r) => r.entry.key === action.key)
      if (!record) return state

      // The rating never reached the server: drop the card (and the copy queued for it) from where it was
      // and show it again right now, in front of the current card.
      const isRecord = (e: Entry) => e.key === record.entry.key || e.key === record.requeuedKey
      const droppedBefore = state.deck.slice(0, state.index).filter(isRecord).length
      const at = state.index - droppedBefore
      const deck = state.deck.filter((e) => !isRecord(e))
      deck.splice(at, 0, { ...record.entry, reviewId: action.reviewId })

      return {
        deck,
        index: at,
        flipped: false,
        history: state.history.filter((r) => r !== record),
        leaving: null,
      }
    }

    case 'intervals':
      return {
        ...state,
        deck: state.deck.map((e) => (e.key === action.key ? { ...e, intervals: action.intervals } : e)),
      }

    case 'leftScreen':
      return state.leaving ? { ...state, leaving: null } : state
  }
}

export const currentEntry = (state: SessionState): Entry | undefined => state.deck[state.index]

