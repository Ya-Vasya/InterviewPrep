import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api, TOPICS, type StudySummary, type Topic } from '../../api/client'
import { formatDueIn } from './format'
import { parseTopics } from './StudySessionPage'
import './study.css'

/** What a session for the chosen topics would contain, with the shared daily limits applied once. */
export function selectionTotals(summary: StudySummary, selected: ReadonlySet<Topic>) {
  const rows = summary.topics.filter((t) => selected.has(t.topic))
  const nextDue = rows
    .map((t) => t.nextDueAt)
    .filter((d): d is string => d !== null)
    .sort()[0]

  return {
    due: Math.min(rows.reduce((sum, t) => sum + t.dueCount, 0), summary.reviewsRemainingToday),
    fresh: Math.min(rows.reduce((sum, t) => sum + t.newCount, 0), summary.newRemainingToday),
    nextDue,
  }
}

export function StudySetupPage() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [selected, setSelected] = useState<ReadonlySet<Topic>>(() => new Set(parseTopics(params.getAll('topic'))))

  const summary = useQuery({ queryKey: ['study', 'summary'], queryFn: api.getStudySummary })

  const toggle = (topic: Topic) =>
    setSelected((current) => {
      const next = new Set(current)
      if (!next.delete(topic)) next.add(topic)
      return next
    })

  const start = () => {
    const query = new URLSearchParams(TOPICS.filter((t) => selected.has(t)).map((t) => ['topic', t]))
    navigate(`/study/session?${query}`)
  }

  if (summary.isPending) return <p>Loading…</p>
  if (summary.isError)
    return <p className="error">Couldn't reach the API ({summary.error.message}). Is the backend running?</p>

  const totals = selectionTotals(summary.data, selected)
  const available = totals.due + totals.fresh
  const canStart = selected.size > 0 && available > 0

  return (
    <>
      <h1>Study</h1>

      <fieldset className="study-topics">
        <legend>Topics</legend>
        {summary.data.topics.map((t) => (
          <label key={t.topic} className="study-topic">
            <input type="checkbox" checked={selected.has(t.topic)} onChange={() => toggle(t.topic)} />
            <span className="study-topic-name">{t.name}</span>
            <span className="study-count" title="Cards due now">
              {t.dueCount} due
            </span>
            <span className="study-count" title="New questions you can start today">
              {t.newCount} new
            </span>
          </label>
        ))}
      </fieldset>

      <div className="study-start-row">
        <button type="button" className="study-start" disabled={!canStart} onClick={start}>
          {selected.size === 0 ? 'Choose a topic' : canStart ? `Start · ${available} cards` : 'Nothing due'}
        </button>
        {selected.size > 0 && !canStart && (
          <p className="study-note">
            {totals.nextDue ? `Next card due ${formatDueIn(totals.nextDue)}.` : 'No cards are scheduled yet.'}
          </p>
        )}
      </div>

      <p className="study-note">
        Daily limits: {summary.data.newRemainingToday} new cards and {summary.data.reviewsRemainingToday} reviews left
        today.
      </p>
    </>
  )
}
