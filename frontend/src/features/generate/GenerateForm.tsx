import { useState, type FormEvent } from 'react'
import {
  MAX_FOCUS_LENGTH,
  MAX_GENERATED_QUESTIONS,
  TOPICS,
  TOPIC_NAMES,
  type Difficulty,
  type GenerateQuestionsRequest,
  type Topic,
} from '../../api/client'
import { AiUsage } from '../ai/AiUsage'
import { Spinner } from './icons'

const DIFFICULTIES: readonly Difficulty[] = ['Junior', 'Middle', 'Senior']
const DEFAULT_COUNT = 5

interface Props {
  running: boolean
  onSubmit: (request: GenerateQuestionsRequest) => void
}

/** What to generate: topic and difficulty, how many (1-10), and an optional tag and focus. */
export function GenerateForm({ running, onSubmit }: Props) {
  const [topic, setTopic] = useState<Topic>('CSharp')
  const [difficulty, setDifficulty] = useState<Difficulty>('Middle')
  const [count, setCount] = useState(String(DEFAULT_COUNT))
  const [tag, setTag] = useState('')
  const [focus, setFocus] = useState('')

  function submit(e: FormEvent) {
    e.preventDefault()
    const parsed = Math.round(Number(count))
    const clamped = Math.min(MAX_GENERATED_QUESTIONS, Math.max(1, Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_COUNT))
    setCount(String(clamped))

    onSubmit({
      topic,
      difficulty,
      count: clamped,
      tag: tag.trim() || undefined,
      focus: focus.trim() || undefined,
    })
  }

  return (
    <form className="generate-form" onSubmit={submit} aria-label="Generate questions">
      <div className="generate-fields">
        <label>
          Topic
          <select value={topic} onChange={(e) => setTopic(e.target.value as Topic)} disabled={running}>
            {TOPICS.map((t) => (
              <option key={t} value={t}>
                {TOPIC_NAMES[t]}
              </option>
            ))}
          </select>
        </label>

        <label>
          Difficulty
          <select value={difficulty} onChange={(e) => setDifficulty(e.target.value as Difficulty)} disabled={running}>
            {DIFFICULTIES.map((d) => (
              <option key={d} value={d}>
                {d}
              </option>
            ))}
          </select>
        </label>

        <label>
          How many
          <input
            type="number"
            min={1}
            max={MAX_GENERATED_QUESTIONS}
            value={count}
            onChange={(e) => setCount(e.target.value)}
            disabled={running}
          />
        </label>

        <label>
          Tag (optional)
          <input value={tag} maxLength={50} onChange={(e) => setTag(e.target.value)} disabled={running} placeholder="e.g. async" />
        </label>

        <label className="generate-focus">
          Focus (optional)
          <input
            value={focus}
            maxLength={MAX_FOCUS_LENGTH}
            onChange={(e) => setFocus(e.target.value)}
            disabled={running}
            placeholder="e.g. distributed caching"
          />
        </label>
      </div>

      <div className="generate-actions">
        <button type="submit" className="primary" disabled={running}>
          {running ? (
            <>
              <Spinner /> Generating…
            </>
          ) : (
            'Generate'
          )}
        </button>
        <AiUsage />
      </div>
    </form>
  )
}
