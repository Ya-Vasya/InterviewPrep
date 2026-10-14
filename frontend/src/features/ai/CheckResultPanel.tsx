import type { CheckAnswerResult, Verdict } from '../../api/client'
import './ai.css'

export function VerdictLabel({ verdict, score }: { verdict: Verdict; score: number }) {
  return (
    <span className={`ai-verdict ai-verdict-${verdict.toLowerCase()}`}>
      {verdict} · {score}/100
    </span>
  )
}

/** The AI's verdict on the typed answer, shown beside the reference answer. */
export function CheckResultPanel({ result }: { result: CheckAnswerResult }) {
  return (
    <section className="ai-result" aria-label="AI verdict">
      <h3>
        <VerdictLabel verdict={result.verdict} score={result.score} />
      </h3>
      <p>{result.feedback}</p>
      {result.covered.length > 0 && (
        <>
          <h4>You covered</h4>
          <ul>
            {result.covered.map((point) => (
              <li key={point}>{point}</li>
            ))}
          </ul>
        </>
      )}
      {result.missing.length > 0 && (
        <>
          <h4>You missed</h4>
          <ul>
            {result.missing.map((point) => (
              <li key={point}>{point}</li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}
