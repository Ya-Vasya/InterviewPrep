import { useEffect, useReducer, useRef, useState } from 'react'
import { useBlocker } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type GenerateQuestionsRequest } from '../../api/client'
import { AI_STATUS_KEY, useAiStatus } from '../ai/useAiStatus'
import { newGuid } from '../study/format'
import { DraftDetail } from './DraftDetail'
import { DraftPanel } from './DraftPanel'
import {
  generateReducer,
  hasUnsavedDrafts,
  initialGenerateState,
  pendingCount,
  type DraftItem,
} from './draftsReducer'
import { GenerateForm } from './GenerateForm'
import { Spinner } from './icons'
import { useResizablePanel } from './useResizablePanel'
import './generate.css'

export function GenerateQuestionsPage() {
  const status = useAiStatus()

  if (status.isPending) return <p>Loading…</p>

  if (!status.data?.enabled) {
    return (
      <>
        <h1>Generate questions</h1>
        <p>
          AI is not configured, so questions cannot be generated. Set an Anthropic API key for the backend to turn it
          on; the rest of the app works without it.
        </p>
      </>
    )
  }

  return <GenerateWorkspace />
}

function GenerateWorkspace() {
  const [state, dispatch] = useReducer(generateReducer, initialGenerateState)
  const [editingId, setEditingId] = useState<string | null>(null)
  const lastRequest = useRef<GenerateQuestionsRequest | null>(null)
  const queryClient = useQueryClient()
  const { width, separatorProps } = useResizablePanel()

  const generate = useMutation({
    mutationFn: (request: GenerateQuestionsRequest) => api.generateQuestions(request),
    // A failed call counts towards the daily cap too, so the counter is refreshed either way.
    onSettled: () => queryClient.invalidateQueries({ queryKey: AI_STATUS_KEY }),
  })

  const accept = useMutation({
    mutationFn: (batch: DraftItem[]) => api.acceptDrafts(batch.map((i) => i.draft)),
    onSuccess: (result, batch) => {
      const errorsByIndex = new Map(result.rejected.map((r) => [r.index, r.errors]))
      dispatch({ type: 'accepted', ids: batch.filter((_, i) => !errorsByIndex.has(i)).map((i) => i.id) })
      dispatch({
        type: 'rejected',
        errors: Object.fromEntries(batch.flatMap((item, i) => (errorsByIndex.has(i) ? [[item.id, errorsByIndex.get(i)!]] : []))),
      })

      // The new questions show up in the question list, the topic counts and the study queue.
      queryClient.invalidateQueries({ queryKey: ['questions'] })
      queryClient.invalidateQueries({ queryKey: ['topics'] })
      queryClient.invalidateQueries({ queryKey: ['study'] })
    },
  })

  const pending = pendingCount(state)
  const unsaved = hasUnsavedDrafts(state)
  const running = state.run.phase === 'running'

  // Drafts live only in this page, so leaving it (in the app, or by closing or refreshing) loses the pending ones.
  const blocker = useBlocker(unsaved)
  useEffect(() => {
    if (blocker.state !== 'blocked') return
    if (window.confirm('You have drafts that are not accepted yet. Leave and lose them?')) blocker.proceed()
    else blocker.reset()
  }, [blocker])

  useEffect(() => {
    if (!unsaved) return
    const warn = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [unsaved])

  function run(request: GenerateQuestionsRequest) {
    lastRequest.current = request
    dispatch({ type: 'start', count: request.count ?? 5 })
    setEditingId(null)
    generate.mutate(request, {
      onSuccess: (result) =>
        dispatch({
          type: 'succeeded',
          drafts: result.drafts,
          ids: result.drafts.map(() => newGuid()),
          duplicatesDropped: result.duplicatesDropped,
        }),
      onError: (error) => dispatch({ type: 'failed', message: error.message }),
    })
  }

  function submit(request: GenerateQuestionsRequest) {
    if (pending > 0 && !window.confirm(`Replace ${pending} pending ${pending === 1 ? 'draft' : 'drafts'}? They will be lost.`)) {
      return
    }
    run(request)
  }

  const selected = state.items.find((i) => i.id === state.selectedId) ?? null
  const acceptItems = (items: DraftItem[]) => items.length > 0 && accept.mutate(items)

  return (
    <>
      <h1>Generate questions</h1>

      <div className="generate-layout" style={{ '--panel-width': `${width}px` } as React.CSSProperties}>
        <DraftPanel
          items={state.items}
          run={state.run}
          selectedId={state.selectedId}
          saving={accept.isPending}
          onSelect={(id) => {
            dispatch({ type: 'select', id })
            setEditingId(null)
          }}
          onEdit={(id) => {
            dispatch({ type: 'select', id })
            setEditingId(id)
          }}
          onDiscard={(id) => dispatch({ type: 'discard', id })}
          onAcceptAll={() => acceptItems(state.items.filter((i) => i.status === 'pending'))}
        />

        <div className="generate-separator" {...separatorProps} />

        <section className="generate-main">
          <GenerateForm running={running} onSubmit={submit} />

          {state.run.phase === 'failed' && (
            <div className="generate-error" role="alert">
              <p className="error">{state.run.message}</p>
              <button type="button" onClick={() => lastRequest.current && run(lastRequest.current)}>
                Retry
              </button>
            </div>
          )}

          {running && (
            <p className="generate-progress">
              <Spinner /> Writing your questions. This can take up to a minute or two.
            </p>
          )}

          {accept.error && (
            <p className="error" role="alert">
              {accept.error.message}
            </p>
          )}

          {state.run.phase === 'idle' && state.duplicatesDropped > 0 && (
            <p className="generate-note" role="status">
              {state.duplicatesDropped} skipped as {state.duplicatesDropped === 1 ? 'a duplicate' : 'duplicates'} of existing
              questions.
            </p>
          )}

          {selected ? (
            <DraftDetail
              key={selected.id}
              item={selected}
              editing={editingId === selected.id}
              saving={accept.isPending}
              onEditingChange={(editing) => setEditingId(editing ? selected.id : null)}
              onChange={(patch) => dispatch({ type: 'edit', id: selected.id, patch })}
              onAccept={() => acceptItems([selected])}
              onDiscard={() => dispatch({ type: 'discard', id: selected.id })}
            />
          ) : (
            !running && <p className="draft-empty">Select a draft from the list to review it.</p>
          )}
        </section>
      </div>
    </>
  )
}
