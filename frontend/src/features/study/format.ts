/** "now", "1 d", "6 d", "3 mo", "1.5 y": the label on a rating button. */
export function formatInterval(days: number): string {
  if (days <= 0) return 'now'
  if (days < 30) return `${days} d`
  if (days < 365) return `${Math.round(days / 30)} mo`
  return `${(days / 365).toFixed(1).replace(/\.0$/, '')} y`
}

/** "in 5 hours", "tomorrow", "in 3 days": when the next card comes due. */
export function formatDueIn(iso: string, now: Date = new Date()): string {
  const ms = new Date(iso).getTime() - now.getTime()
  const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' })
  const minutes = Math.round(ms / 60_000)
  if (Math.abs(minutes) < 60) return rtf.format(Math.max(minutes, 1), 'minute')
  const hours = Math.round(ms / 3_600_000)
  if (Math.abs(hours) < 24) return rtf.format(hours, 'hour')
  return rtf.format(Math.round(ms / 86_400_000), 'day')
}

/**
 * A random v4 GUID. crypto.randomUUID only exists in secure contexts (https or localhost), so opening the
 * dev server from a phone over plain http would break it; getRandomValues works everywhere.
 */
export function newGuid(): string {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID()
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}
