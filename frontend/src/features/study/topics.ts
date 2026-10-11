import { TOPICS, type Topic } from '../../api/client'

/** Topics from `?topic=` in the URL, so a reload resumes the same selection. No topic means all of them. */
export function parseTopics(values: string[]): Topic[] {
  const picked = TOPICS.filter((t) => values.some((v) => v.toLowerCase() === t.toLowerCase()))
  return picked.length > 0 ? picked : [...TOPICS]
}
