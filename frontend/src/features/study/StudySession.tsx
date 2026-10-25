import { useEffect, useReducer, useRef, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api, type CheckAnswerResult, type Rating, type ReviewRequest, type StudyQueueItem, type Topic } from '../../api/client'
import { AnswerCheckForm } from '../ai/AnswerCheckForm'
import { AttemptHistory } from '../ai/AttemptHistory'
import { CheckResultPanel } from '../ai/CheckResultPanel'
import { NotePanel } from '../ai/NotePanel'
import { FlashCard } from './FlashCard'
import { newGuid } from './format'
import { RatingButtons } from './RatingButtons'
import { currentEntry, initSession, sessionReducer } from './sessionReducer'
import { useStudyShortcuts } from './useStudyShortcuts'

/** Matches the leave animation in study.css. */
const LEAVE_MS = 320

interface Props {
  items: StudyQueueItem[]
  /** The topics this session was started with, to link back to the setup screen. */
  topics: readonly Topic[]
}

export function StudySession({ items, topics }: Props) {
  const [state, dispatch] = useReducer(sessionReducer, items, (initial) => initSession(initial, newGuid))
  const [error, setError] = useState<string | null>(null)
  const [undoing, setUndoing] = useState(false)

  const queryClient = useQueryClient()
  const refreshSummary = () => queryClient.invalidateQueries({ queryKey: ['study', 'summary'] })
  const rateMutation = useMutation({ mutationFn: (review: ReviewRequest) => api.postReview(review), onSuccess: refreshSummary })
  const undoMutation = useMutation({ mutationFn: (reviewId: string) => api.undoReview(reviewId), onSuccess: refreshSummary })

  const entry = currentEntry(state)
  const finished = entry === undefined

  // The AI check of the card showing now. It belongs to one showing of a card, so it is keyed by the entry.
  const [check, setCheck] = useState<{ entryKey: string; result: CheckAnswerResult } | null>(null)
  const currentCheck = entry && check?.entryKey === entry.key ? check.result : null

  // Latest state for the async handlers below, and one-shot guards keyed by reviewId.
  const stateRef = useRef(state)
  useEffect(() => {
    stateRef.current = state
  })
  const ratedReviewIds = useRef(new Set<string>())
  const inFlight = useRef(new Map<string, Promise<unknown>>())

  const flip = () => dispatch({ type: 'flip' })

  const rate = (rating: Rating) => {
    if (!entry || !state.flipped || undoing || ratedReviewIds.current.has(entry.reviewId)) return
    ratedReviewIds.current.add(entry.reviewId)
    setError(null)

    const requeueKey = newGuid()
    dispatch({ type: 'rate', rating, requeueKey, requeueReviewId: newGuid() })

    const request = rateMutation
      .mutateAsync({
        questionId: entry.item.questionId,
        rating,
        reviewId: entry.reviewId,
        attemptId: currentCheck?.attemptId,
      })
      .then((result) => {
        // The re-queued copy of an Again card needs the intervals of its new state.
        if (rating === 'Again') dispatch({ type: 'intervals', key: requeueKey, intervals: result.nextIntervals })
      })
      .catch(() => {
        ratedReviewIds.current.delete(entry.reviewId)
        dispatch({ type: 'rateFailed', key: entry.key, reviewId: newGuid() })
        setError("Couldn't save your rating. The card is back in the queue; try rating it again.")
      })
      .finally(() => inFlight.current.delete(entry.reviewId))
    inFlight.current.set(entry.reviewId, request)
  }

  const undo = async () => {
    const record = state.history.at(-1)
    if (!record || undoing) return
    setUndoing(true)
    setError(null)
    try {
      // The rating may still be on its way to the server; undo only makes sense after it landed.
      await inFlight.current.get(record.entry.reviewId)
      if (stateRef.current.history.at(-1)?.entry.reviewId !== record.entry.reviewId) return // it failed and was restored already
      await undoMutation.mutateAsync(record.entry.reviewId)
      ratedReviewIds.current.delete(record.entry.reviewId)
      setCheck(null)
      dispatch({ type: 'undo', reviewId: newGuid() })
    } catch {
      setError("Couldn't undo the last rating. Check your connection and try again.")
    } finally {
      setUndoing(false)
    }
  }

  useStudyShortcuts({ enabled: !finished && !undoing, flipped: state.flipped, onFlip: flip, onRate: rate })

  // Let the rated card finish flying off screen, then drop it.
  useEffect(() => {
    if (!state.leaving) return
    const timer = setTimeout(() => dispatch({ type: 'leftScreen' }), LEAVE_MS)
    return () => clearTimeout(timer)
  }, [state.leaving])

  // A new card starts at the top, even if the previous one was rated far down the page.
  const currentReviewId = entry?.reviewId
  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [currentReviewId])

  const setupLink = `/study?${new URLSearchParams(topics.map((t) => ['topic', t]))}`
  const total = state.deck.length

  return (
    <section className="study-session" aria-label="Study session">
      <div className="study-toolbar">
        <Link to={setupLink} className="study-back">
          ← Setup
        </Link>
        {!finished && (
          <span className="study-progress" aria-live="polite">
            Card {state.index + 1} of {total}
          </span>
        )}
        <button
          type="button"
          className="study-undo"
          onClick={(e) => {
            e.currentTarget.blur() // keep Space from re-clicking Undo
            void undo()
          }}
          disabled={state.history.length === 0 || undoing}
        >
          ↶ Undo
        </button>
      </div>

      {total > 0 && (
        <div className="study-bar" aria-hidden="true">
          <div style={{ width: `${(state.index / total) * 100}%` }} />
        </div>
      )}

      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}

      {entry ? (
        <>
          <FlashCard
            entry={entry}
            flipped={state.flipped}
            onFlip={flip}
            behind={state.deck.length - state.index - 1}
            leaving={state.leaving}
          />
          {!state.flipped && (
            <AnswerCheckForm
              key={entry.key}
              questionId={entry.item.questionId}
              onChecked={(result) => {
                // The AI can answer after the user flipped, rated and moved on; the result then belongs to a
                // card that is no longer showing and must not flip the current one.
                if (currentEntry(stateRef.current)?.key !== entry.key) return
                setCheck({ entryKey: entry.key, result })
                flip()
              }}
            />
          )}
          {state.flipped && <NotePanel questionId={entry.item.questionId} />}
          {state.flipped && currentCheck && <CheckResultPanel result={currentCheck} questionId={entry.item.questionId} />}
          {state.flipped && (
            <AttemptHistory questionId={entry.item.questionId} excludeAttemptId={currentCheck?.attemptId} />
          )}
          {state.flipped && (
            <RatingButtons
              intervals={entry.intervals}
              disabled={undoing}
              onRate={rate}
              suggested={currentCheck?.suggestedRating}
            />
          )}
        </>
      ) : (
        <div className="study-done">
          <h2>{state.history.length === 0 ? 'Nothing due' : 'Session complete'}</h2>
          <p>
            {state.history.length === 0
              ? 'There are no cards to study for these topics right now.'
              : `You rated ${state.history.length} ${state.history.length === 1 ? 'card' : 'cards'}.`}
          </p>
          <Link to="/study" className="study-start">
            Back to study
          </Link>
        </div>
      )}
    </section>
  )
}
