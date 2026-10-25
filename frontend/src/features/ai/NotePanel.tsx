import { useQuery } from '@tanstack/react-query'
import { api } from '../../api/client'
import { Markdown } from '../../components/Markdown'
import './ai.css'
import { noteKey } from './notes'

/**
 * The question's note, shown on the back of the card. A note is the user's own content, so it shows even when
 * AI is not configured. It belongs to the question: it is there before any card exists and survives suspension.
 */
export function NotePanel({ questionId }: { questionId: string }) {
  const note = useQuery({ queryKey: noteKey(questionId), queryFn: () => api.getNote(questionId), retry: false })

  if (!note.data) return null

  return (
    <section className="ai-note" aria-label="Your note">
      <h3>Your note</h3>
      <Markdown>{note.data.text}</Markdown>
    </section>
  )
}
