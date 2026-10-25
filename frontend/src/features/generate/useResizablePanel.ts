import { useRef, useState, type KeyboardEvent, type PointerEvent } from 'react'

export const PANEL_MIN_WIDTH = 220
export const PANEL_DEFAULT_WIDTH = 300
const STEP = 16
const STORAGE_KEY = 'generate.panelWidth'

/** At most half the window. */
const maxWidth = () => Math.max(PANEL_MIN_WIDTH, Math.floor(window.innerWidth / 2))
const clamp = (width: number) => Math.min(maxWidth(), Math.max(PANEL_MIN_WIDTH, Math.round(width)))

function readStored(): number | null {
  try {
    const value = Number(window.localStorage.getItem(STORAGE_KEY))
    return Number.isFinite(value) && value > 0 ? value : null
  } catch {
    return null // storage can be blocked; the default width is fine
  }
}

function store(width: number) {
  try {
    window.localStorage.setItem(STORAGE_KEY, String(width))
  } catch {
    // a per-browser convenience only
  }
}

/**
 * The width of a side panel the user can drag, or resize with the arrow keys. Double-click resets it. The width
 * is remembered in this browser only.
 */
export function useResizablePanel() {
  const [width, setWidth] = useState(() => clamp(readStored() ?? PANEL_DEFAULT_WIDTH))
  const drag = useRef<{ startX: number; startWidth: number } | null>(null)

  const update = (next: number) => {
    const clamped = clamp(next)
    setWidth(clamped)
    store(clamped)
  }

  const separatorProps = {
    role: 'separator',
    'aria-orientation': 'vertical' as const,
    'aria-label': 'Resize drafts panel',
    'aria-valuemin': PANEL_MIN_WIDTH,
    'aria-valuemax': maxWidth(),
    'aria-valuenow': width,
    tabIndex: 0,
    onPointerDown: (e: PointerEvent<HTMLElement>) => {
      e.currentTarget.setPointerCapture?.(e.pointerId)
      drag.current = { startX: e.clientX, startWidth: width }
    },
    onPointerMove: (e: PointerEvent<HTMLElement>) => {
      if (drag.current) setWidth(clamp(drag.current.startWidth + e.clientX - drag.current.startX))
    },
    onPointerUp: (e: PointerEvent<HTMLElement>) => {
      if (!drag.current) return
      update(drag.current.startWidth + e.clientX - drag.current.startX)
      drag.current = null
    },
    onKeyDown: (e: KeyboardEvent<HTMLElement>) => {
      const next =
        e.key === 'ArrowLeft' ? width - STEP
        : e.key === 'ArrowRight' ? width + STEP
        : e.key === 'Home' ? PANEL_MIN_WIDTH
        : e.key === 'End' ? maxWidth()
        : null
      if (next === null) return
      e.preventDefault()
      update(next)
    },
    onDoubleClick: () => update(PANEL_DEFAULT_WIDTH),
  }

  return { width, separatorProps }
}
