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

/** Shape of the backend's error bodies (RFC 9457 ProblemDetails; `errors` is set for validation failures). */
interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  status: number
  /** Field name to messages, present for 400 validation problems. */
  errors?: Record<string, string[]>

  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | undefined
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    // Not JSON (for example an nginx error page); fall back to the status line.
  }
  const message = problem?.detail ?? problem?.title ?? `${response.status} ${response.statusText}`
  return new ApiError(response.status, message, problem?.errors)
}

async function get<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) throw await toApiError(response)
  return response.json() as Promise<T>
}

export const api = {
  getTopics: () => get<TopicSummary[]>('/api/topics'),
  getQuestions: (topic?: Topic) =>
    get<Question[]>(topic ? `/api/questions?topic=${encodeURIComponent(topic)}` : '/api/questions'),
}
