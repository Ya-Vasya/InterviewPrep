import { useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../../api/client'
import './study.css'
import { StudySession } from './StudySession'
import { parseTopics } from './topics'

export function StudySessionPage() {
  const [params] = useSearchParams()
  const topicParams = params.getAll('topic').join(',')
  const topics = useMemo(() => parseTopics(topicParams ? topicParams.split(',') : []), [topicParams])

  // The queue is a snapshot taken when the session starts: never refetched mid-session, dropped on leave.
  const queue = useQuery({
    queryKey: ['study', 'queue', topics],
    queryFn: () => api.getStudyQueue(topics),
    staleTime: Infinity,
    gcTime: 0,
    refetchOnReconnect: false,
  })

  if (queue.isPending) return <p>Loading cards…</p>

  if (queue.isError) {
    return (
      <>
        <p className="error">Couldn't load the study queue ({queue.error.message}). Is the backend running?</p>
        <Link to="/study">Back to study</Link>
      </>
    )
  }

  return <StudySession items={queue.data} topics={topics} />
}
