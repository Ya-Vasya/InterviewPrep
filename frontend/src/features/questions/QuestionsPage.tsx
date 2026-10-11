import { useState } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { api, type Topic } from '../../api/client'
import { QuestionCard } from '../../components/QuestionCard'

export function QuestionsPage() {
  const [topic, setTopic] = useState<Topic | ''>('')

  const topics = useQuery({ queryKey: ['topics'], queryFn: api.getTopics })
  const questions = useInfiniteQuery({
    queryKey: ['questions', topic],
    queryFn: ({ pageParam }) => api.getQuestions({ topic: topic || undefined, offset: pageParam }),
    initialPageParam: 0,
    getNextPageParam: (last) => {
      const next = last.offset + last.items.length
      return next < last.total ? next : undefined
    },
  })

  const topicList = topics.data ?? []
  const items = questions.data?.pages.flatMap((p) => p.items) ?? []
  const total = questions.data?.pages[0]?.total
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
        {items.map((q) => (
          <QuestionCard key={q.id} question={q} topicName={topicList.find((t) => t.topic === q.topic)?.name ?? q.topic} />
        ))}
      </section>

      {total !== undefined && (
        <footer className="list-footer">
          <p>
            Showing {items.length} of {total}
          </p>
          {questions.hasNextPage && (
            <button type="button" onClick={() => questions.fetchNextPage()} disabled={questions.isFetchingNextPage}>
              {questions.isFetchingNextPage ? 'Loading…' : 'Load more'}
            </button>
          )}
        </footer>
      )}
    </>
  )
}
