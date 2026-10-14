import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type Verdict } from '../../api/client'
import { Markdown } from '../../components/Markdown'
import './ai.css'
import { AiUsage } from './AiUsage'
import { SaveAsNote } from './SaveAsNote'
import { AI_STATUS_KEY, useAiStatus } from './useAiStatus'

interface Props {
  attemptId: string
  questionId: string
  verdict: Verdict
  /** Show the usage counter beside the button. Lists of attempts show it once instead. */
  showUsage?: boolean
}

/** "Explain my mistakes" for a partial or incorrect attempt: one-shot explanation, which can be saved as the note. */
export function ExplainMistakes({ attemptId, questionId, verdict, showUsage = false }: Props) {
  const status = useAiStatus()
  const queryClient = useQueryClient()

  const explain = useMutation({
    mutationFn: () => api.explainMistakes(attemptId),
    // Failed calls count towards the daily cap too, so the counter is refreshed either way.
    onSettled: () => queryClient.invalidateQueries({ queryKey: AI_STATUS_KEY }),
  })

  if (!status.data?.enabled || verdict === 'Correct') return null

  return (
    <div className="ai-explain">
      {!explain.data && (
        <div className="ai-check-actions">
          <button type="button" disabled={explain.isPending} onClick={() => explain.mutate()}>
            {explain.isPending ? 'Explaining…' : 'Explain my mistakes'}
          </button>
          {showUsage && <AiUsage />}
        </div>
      )}

      {explain.error && (
        <p className="error" role="alert">
          {explain.error.message}
        </p>
      )}

      {explain.data && (
        <section className="ai-explanation" aria-label="Explanation">
          <Markdown>{explain.data.explanation}</Markdown>
          <SaveAsNote questionId={questionId} text={explain.data.explanation} />
        </section>
      )}
    </div>
  )
}
