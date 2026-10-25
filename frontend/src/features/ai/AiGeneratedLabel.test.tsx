import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { Question, StudyQueueItem } from '../../api/client'
import { QuestionCard } from '../../components/QuestionCard'
import { FlashCard } from '../study/FlashCard'

const LABEL = 'AI-generated, verify before relying on it'

const question = (source: Question['source']): Question => ({
  id: 'q1', topic: 'CSharp', text: 'What is a Task?', answer: 'A promise of work.', difficulty: 'Middle', tags: [], source,
})

const queueItem = (source: StudyQueueItem['source']): StudyQueueItem => ({
  questionId: 'q1', cardId: null, topic: 'CSharp', difficulty: 'Middle', text: 'What is a Task?',
  answer: 'A promise of work.', tags: [], source, isNew: true, intervals: { again: 0, hard: 1, good: 6, easy: 8 },
})

const renderCard = (source: StudyQueueItem['source'], flipped = true) =>
  render(
    <FlashCard
      entry={{ key: 'k', reviewId: 'r', item: queueItem(source), intervals: null }}
      flipped={flipped}
      onFlip={() => {}}
      behind={0}
      leaving={null}
    />,
  )

describe('the AI-generated label', () => {
  it('is on AI-sourced questions in the question list, and only on those', () => {
    const { unmount } = render(<QuestionCard question={question('Ai')} topicName="C#" />)
    expect(screen.getByText(LABEL)).toBeInTheDocument()
    unmount()

    render(<QuestionCard question={question('Seed')} topicName="C#" />)
    expect(screen.queryByText(LABEL)).not.toBeInTheDocument()
  })

  it('is on the back of an AI-sourced study card', () => {
    renderCard('Ai')

    expect(screen.getByText(LABEL)).toBeInTheDocument()
  })

  it('is not on the back of other study cards, nor on the front of any', () => {
    const { unmount } = renderCard('Manual')
    expect(screen.queryByText(LABEL)).not.toBeInTheDocument()
    unmount()

    renderCard('Ai', false)
    expect(screen.queryByText(LABEL)).not.toBeInTheDocument()
  })
})
