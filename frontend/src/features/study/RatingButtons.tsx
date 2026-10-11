import type { Rating, RatingIntervals } from '../../api/client'
import { formatInterval } from './format'
import { RATINGS } from './useStudyShortcuts'

interface Props {
  /** Null while the server has not yet returned the intervals of a re-queued card. */
  intervals: RatingIntervals | null
  disabled: boolean
  onRate: (rating: Rating) => void
}

/** Rendered only for a flipped card, below the answer: the user has to scroll past it to rate. */
export function RatingButtons({ intervals, disabled, onRate }: Props) {
  return (
    <div className="study-ratings" role="group" aria-label="How well did you remember it?">
      {RATINGS.map((rating, i) => {
        const days = intervals?.[rating.toLowerCase() as keyof RatingIntervals]
        return (
          <button
            key={rating}
            type="button"
            className={`study-rate rate-${rating.toLowerCase()}`}
            disabled={disabled}
            onClick={() => onRate(rating)}
          >
            {days === undefined ? rating : `${rating} · ${formatInterval(days)}`}
            <kbd aria-hidden="true">{i + 1}</kbd>
          </button>
        )
      })}
    </div>
  )
}
