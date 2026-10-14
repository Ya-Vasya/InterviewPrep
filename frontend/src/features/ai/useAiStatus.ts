import { useQuery } from '@tanstack/react-query'
import { api } from '../../api/client'

export const AI_STATUS_KEY = ['ai', 'status'] as const

/**
 * Whether AI is configured, and today's usage. While loading or when the call fails the AI controls simply
 * stay hidden: the rest of the app works without AI.
 */
export function useAiStatus() {
  return useQuery({ queryKey: AI_STATUS_KEY, queryFn: api.getAiStatus, staleTime: 60_000, retry: false })
}
