import { useEffect, useRef } from 'react'
import type { Rating } from '../../api/client'

export const RATINGS: readonly Rating[] = ['Again', 'Hard', 'Good', 'Easy']

interface Options {
  /** False while there is no card to act on (loading, finished). */
  enabled: boolean
  flipped: boolean
  onFlip: () => void
  onRate: (rating: Rating) => void
}

const isTextField = (el: EventTarget | null) =>
  el instanceof HTMLElement && (el.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName))

/**
 * Space flips the card, 1-4 rate it (Again, Hard, Good, Easy). Ratings are ignored until the card is
 * flipped, mirroring the buttons, which do not exist before that.
 */
export function useStudyShortcuts({ enabled, flipped, onFlip, onRate }: Options) {
  // Always call the latest callbacks without re-subscribing on every render.
  const latest = useRef({ enabled, flipped, onFlip, onRate })
  useEffect(() => {
    latest.current = { enabled, flipped, onFlip, onRate }
  })

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const { enabled, flipped, onFlip, onRate } = latest.current
      if (!enabled || e.repeat || e.ctrlKey || e.metaKey || e.altKey || isTextField(e.target)) return

      if (e.key === ' ') {
        // On a focused button, Space already means "click it": leave that alone.
        if (e.target instanceof HTMLElement && e.target.closest('button, a[href]')) return
        e.preventDefault()
        if (!flipped) onFlip()
        return
      }

      const rating = RATINGS[Number(e.key) - 1]
      if (rating && /^[1-4]$/.test(e.key) && flipped) onRate(rating)
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])
}
