import type { Rating } from '../../api/client'
import { TOPIC_NAMES } from '../../api/client'
import { Markdown } from '../../components/Markdown'
import { AiGeneratedLabel } from '../ai/AiGeneratedLabel'
import type { Entry } from './sessionReducer'

interface Props {
  entry: Entry
  flipped: boolean
  onFlip: () => void
  /** How many cards are queued behind this one (drives the peeking edges of the stack). */
  behind: number
  /** The card that was just rated, flying off screen. */
  leaving: { entry: Entry; rating: Rating } | null
}

export function FlashCard({ entry, flipped, onFlip, behind, leaving }: Props) {
  const { item } = entry

  return (
    <div className="study-stack" data-behind={Math.min(behind, 2)}>
      {leaving && (
        <div className={`study-card study-card-leaving leave-${leaving.rating.toLowerCase()}`} aria-hidden="true">
          <p className="study-question">{leaving.entry.item.text}</p>
        </div>
      )}

      <article className="study-card" aria-label="Flashcard">
        <header className="study-card-meta">
          <span>{TOPIC_NAMES[item.topic]}</span>
          {item.isNew && <span className="study-badge">New</span>}
          <span className={`level level-${item.difficulty.toLowerCase()}`}>{item.difficulty}</span>
        </header>

        {flipped ? (
          // A new key replays the flip animation when the face changes.
          <div className="study-face study-face-back" key="back">
            <p className="study-question study-question-small">{item.text}</p>
            <hr />
            {item.source === 'Ai' && (
              <p>
                <AiGeneratedLabel />
              </p>
            )}
            <Markdown>{item.answer}</Markdown>
          </div>
        ) : (
          <button type="button" className="study-face study-face-front" key="front" onClick={onFlip}>
            <span className="study-question">{item.text}</span>
            <span className="study-hint">Tap the card or press Space to show the answer</span>
          </button>
        )}
      </article>
    </div>
  )
}
