import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type CheckAnswerResult } from '../../api/client'
import './ai.css'
import { AI_STATUS_KEY, useAiStatus } from './useAiStatus'

/** Mirrors CheckAnswerLimits.MaxAnswerLength on the backend. */
export const MAX_ANSWER_LENGTH = 4000

interface Props {
  questionId: string
  onChecked: (result: CheckAnswerResult) => void
}

/** Optional: the user types an answer before revealing the reference and has the AI grade it. */
export function AnswerCheckForm({ questionId, onChecked }: Props) {
  const status = useAiStatus()
  const queryClient = useQueryClient()
  const [answer, setAnswer] = useState('')

  const check = useMutation({
    mutationFn: () => api.checkAnswer(questionId, answer.trim()),
    onSuccess: onChecked,
    // Failed calls count towards the daily cap too, so the counter is refreshed either way.
    onSettled: () => queryClient.invalidateQueries({ queryKey: AI_STATUS_KEY }),
  })

  if (!status.data?.enabled) return null

  const canCheck = answer.trim().length > 0 && !check.isPending

  return (
    <form
      className="ai-check"
      onSubmit={(e) => {
        e.preventDefault()
        if (canCheck) check.mutate()
      }}
    >
      <label htmlFor="ai-answer">Your answer (optional)</label>
      <textarea
        id="ai-answer"
        rows={4}
        maxLength={MAX_ANSWER_LENGTH}
        value={answer}
        placeholder="Type what you remember, then let the AI grade it before you see the reference answer."
        onChange={(e) => setAnswer(e.target.value)}
      />
      <div className="ai-check-actions">
        <button type="submit" disabled={!canCheck}>
          {check.isPending ? 'Checking…' : 'Check with AI'}
        </button>
        <span className="ai-usage">
          AI calls today: {status.data.usedToday} / {status.data.dailyCap}
        </span>
      </div>
      {check.error && (
        <p className="error" role="alert">
          {check.error.message}
        </p>
      )}
    </form>
  )
}
