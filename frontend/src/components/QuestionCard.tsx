import { useState } from 'react'
import type { Question } from '../api/client'
import { AiGeneratedLabel } from '../features/ai/AiGeneratedLabel'
import { Markdown } from './Markdown'

export function QuestionCard({ question, topicName }: { question: Question; topicName: string }) {
  const [showAnswer, setShowAnswer] = useState(false)

  return (
    <article className="card">
      <header className="card-header">
        <span className="topic">{topicName}</span>
        <span className={`level level-${question.difficulty.toLowerCase()}`}>{question.difficulty}</span>
      </header>
      <p className="question">{question.text}</p>
      {question.source === 'Ai' && (
        <p>
          <AiGeneratedLabel />
        </p>
      )}
      {showAnswer ? (
        <Markdown>{question.answer}</Markdown>
      ) : (
        <button type="button" onClick={() => setShowAnswer(true)}>
          Show answer
        </button>
      )}
    </article>
  )
}
