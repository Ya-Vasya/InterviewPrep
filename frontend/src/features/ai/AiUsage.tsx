import './ai.css'
import { useAiStatus } from './useAiStatus'

/** Today's AI calls against the daily cap. Shown next to the AI controls, not in the global nav. */
export function AiUsage() {
  const status = useAiStatus()
  if (!status.data?.enabled) return null

  return (
    <span className="ai-usage">
      AI calls today: {status.data.usedToday} / {status.data.dailyCap}
    </span>
  )
}
