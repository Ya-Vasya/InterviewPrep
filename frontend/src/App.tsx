import { useEffect, useState } from 'react'
import { api, type Question, type Topic, type TopicSummary } from './api/client'
import { QuestionCard } from './components/QuestionCard'

export default function App() {
  const [topics, setTopics] = useState<TopicSummary[]>([])
  const [topic, setTopic] = useState<Topic | ''>('')
  const [questions, setQuestions] = useState<Question[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.getTopics().then(setTopics).catch((e: Error) => setError(e.message))
  }, [])

  useEffect(() => {
    setLoading(true)
    api
      .getQuestions(topic || undefined)
      .then((qs) => {
        setQuestions(qs)
        setError(null)
      })
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [topic])

  return (
    <main className="container">
      <h1>.NET Interview Prep</h1>

      <label className="filter">
        Topic
        <select value={topic} onChange={(e) => setTopic(e.target.value as Topic | '')}>
          <option value="">All topics</option>
          {topics.map((t) => (
            <option key={t.topic} value={t.topic}>
              {t.name} ({t.questionCount})
            </option>
          ))}
        </select>
      </label>

      {error && <p className="error">Couldn't reach the API ({error}). Is the backend running?</p>}
      {loading && !error && <p>Loading…</p>}

      <section className="list">
        {questions.map((q) => (
          <QuestionCard key={q.id} question={q} topicName={topics.find((t) => t.topic === q.topic)?.name ?? q.topic} />
        ))}
      </section>
    </main>
  )
}
