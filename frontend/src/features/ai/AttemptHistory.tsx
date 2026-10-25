import { useQuery } from '@tanstack/react-query'
import { api } from '../../api/client'
import './ai.css'
import { AiUsage } from './AiUsage'
import { VerdictLabel } from './CheckResultPanel'
import { ExplainMistakes } from './ExplainMistakes'
import { useAiStatus } from './useAiStatus'

interface Props {
  questionId: string
  /** The attempt just made in this showing of the card; it is already displayed above, so it is left out. */
  excludeAttemptId?: string
}

/** Earlier answers the user typed for this question, newest first, so mistakes can be revisited. */
export function AttemptHistory({ questionId, excludeAttemptId }: Props) {
  const status = useAiStatus()
  const attempts = useQuery({
    queryKey: ['ai', 'attempts', questionId],
    queryFn: () => api.getAnswerAttempts(questionId),
    enabled: status.data?.enabled === true,
  })

  const previous = (attempts.data?.items ?? []).filter((a) => a.id !== excludeAttemptId)
  if (previous.length === 0) return null

  return (
    <details className="ai-history">
      <summary>Previous attempts ({previous.length})</summary>
      {previous.some((a) => a.verdict !== 'Correct') && <AiUsage />}
      <ul>
        {previous.map((a) => (
          <li key={a.id}>
            <VerdictLabel verdict={a.verdict} score={a.score} />{' '}
            <time dateTime={a.createdAt}>{new Date(a.createdAt).toLocaleDateString()}</time>
            <blockquote>{a.answer}</blockquote>
            <p>{a.feedback}</p>
            <ExplainMistakes attemptId={a.id} questionId={questionId} verdict={a.verdict} />
          </li>
        ))}
      </ul>
    </details>
  )
}
