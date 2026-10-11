import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, type Topic } from '../../api/client'
import { QuestionCard } from '../../components/QuestionCard'

export function QuestionsPage() {
  const [topic, setTopic] = useState<Topic | ''>('')

  const topics = useQuery({ queryKey: ['topics'], queryFn: api.getTopics })
  const questions = useQuery({
    queryKey: ['questions', topic],
    queryFn: () => api.getQuestions(topic || undefined),
  })

  const topicList = topics.data ?? []
  const error = topics.error ?? questions.error

  return (
    <>
      <h1>Questions</h1>

      <label className="filter">
        Topic
        <select value={topic} onChange={(e) => setTopic(e.target.value as Topic | '')}>
          <option value="">All topics</option>
          {topicList.map((t) => (
            <option key={t.topic} value={t.topic}>
              {t.name} ({t.questionCount})
            </option>
          ))}
        </select>
      </label>

      {error && <p className="error">Couldn't reach the API ({error.message}). Is the backend running?</p>}
      {questions.isPending && !error && <p>Loading…</p>}

      <section className="list">
        {questions.data?.map((q) => (
          <QuestionCard key={q.id} question={q} topicName={topicList.find((t) => t.topic === q.topic)?.name ?? q.topic} />
        ))}
      </section>
    </>
  )
}
