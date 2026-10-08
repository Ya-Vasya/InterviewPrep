import { useState } from 'react'
import type { Question } from '../api/client'

export function QuestionCard({ question, topicName }: { question: Question; topicName: string }) {
  const [showAnswer, setShowAnswer] = useState(false)

  return (
    <article className="card">
      <header className="card-header">
        <span className="topic">{topicName}</span>
        <span className={`level level-${question.difficulty.toLowerCase()}`}>{question.difficulty}</span>
      </header>
      <p className="question">{question.text}</p>
      {showAnswer ? (
        <p className="answer">{question.answer}</p>
      ) : (
        <button type="button" onClick={() => setShowAnswer(true)}>
          Show answer
        </button>
      )}
    </article>
  )
}
