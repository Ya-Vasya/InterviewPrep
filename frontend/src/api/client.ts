export type Difficulty = 'Junior' | 'Middle' | 'Senior'
export type Topic = 'CSharp' | 'AspNetCore' | 'Sql' | 'SystemDesign'
export type QuestionSource = 'Seed' | 'Manual' | 'Ai'

export interface TopicSummary {
  topic: Topic
  name: string
  questionCount: number
}

export interface Question {
  id: string
  topic: Topic
  text: string
  answer: string
  difficulty: Difficulty
  tags: string[]
  source: QuestionSource
}

async function get<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`)
  return response.json() as Promise<T>
}

export const api = {
  getTopics: () => get<TopicSummary[]>('/api/topics'),
  getQuestions: (topic?: Topic) =>
    get<Question[]>(topic ? `/api/questions?topic=${encodeURIComponent(topic)}` : '/api/questions'),
}
