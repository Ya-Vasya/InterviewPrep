import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../../api/client'
import './ai.css'
import { noteKey } from './notes'

interface Props {
  questionId: string
  /** The markdown to keep as the note. */
  text: string
}

/** Saves text as the question's note. A question has one note, so replacing it needs the user's confirmation. */
export function SaveAsNote({ questionId, text }: Props) {
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)

  const save = useMutation({
    mutationFn: () => api.saveNote(questionId, text),
    onSuccess: (note) => {
      queryClient.setQueryData(noteKey(questionId), note)
      setConfirming(false)
    },
  })

  // Asks the server rather than trusting what the card showed: the note may have changed since.
  const lookUp = useMutation({
    mutationFn: () => api.getNote(questionId),
    onSuccess: (existing) => (existing ? setConfirming(true) : save.mutate()),
  })

  const error = lookUp.error ?? save.error
  const busy = lookUp.isPending || save.isPending

  return (
    <div className="ai-save-note">
      {save.isSuccess ? (
        <span className="ai-saved">Saved as your note</span>
      ) : (
        <button
          type="button"
          disabled={busy || confirming}
          onClick={() => {
            lookUp.reset()
            save.reset()
            lookUp.mutate()
          }}
        >
          {busy ? 'Saving…' : 'Save as note'}
        </button>
      )}

      {confirming && (
        <div role="alertdialog" aria-label="Replace note?" className="ai-confirm">
          <p>You already have a note for this question. Replace it with this explanation?</p>
          <div className="ai-check-actions">
            <button type="button" disabled={busy} onClick={() => save.mutate()}>
              Replace note
            </button>
            <button type="button" className="ai-secondary" disabled={busy} onClick={() => setConfirming(false)}>
              Cancel
            </button>
          </div>
        </div>
      )}

      {error && (
        <p className="error" role="alert">
          {error.message}
        </p>
      )}
    </div>
  )
}
