import type { DraftItem, RunState } from './draftsReducer'
import { CheckIcon, CrossIcon, PencilIcon, Spinner, WarningIcon } from './icons'

interface Props {
  items: DraftItem[]
  run: RunState
  selectedId: string | null
  /** Disables the buttons that save Drafts while a save is in flight. */
  saving: boolean
  onSelect: (id: string) => void
  onEdit: (id: string) => void
  onDiscard: (id: string) => void
  onAcceptAll: () => void
}

/** The list of Drafts: truncated questions, with hover actions and "Accept all" pinned to the bottom. */
export function DraftPanel({ items, run, selectedId, saving, onSelect, onEdit, onDiscard, onAcceptAll }: Props) {
  const pending = items.filter((i) => i.status === 'pending').length
  const placeholders = run.phase === 'running' ? run.count : 0

  return (
    <aside className="draft-panel" aria-label="Drafts">
      <header className="draft-panel-header">
        <h2>Drafts</h2>
        {items.length > 0 && <span>{pending} pending</span>}
      </header>

      {placeholders > 0 && (
        <p role="status" className="visually-hidden">
          Generating {placeholders} {placeholders === 1 ? 'question' : 'questions'}…
        </p>
      )}

      <ul className="draft-list">
        {Array.from({ length: placeholders }, (_, i) => (
          <li key={`placeholder-${i}`} className="draft-item draft-placeholder" aria-hidden="true">
            <span className="draft-state">
              <Spinner />
            </span>
            <span className="draft-skeleton" />
          </li>
        ))}

        {items.map((item) => {
          const label = item.draft.text
          return (
            <li
              key={item.id}
              className={`draft-item${item.id === selectedId ? ' is-selected' : ''}${item.status === 'accepted' ? ' is-accepted' : ' has-actions'}`}
            >
              <button
                type="button"
                className="draft-select"
                aria-current={item.id === selectedId ? 'true' : undefined}
                title={label}
                onClick={() => onSelect(item.id)}
              >
                <span className="draft-state">
                  {item.status === 'accepted' ? (
                    <span className="draft-saved" role="img" aria-label="Accepted">
                      <CheckIcon />
                    </span>
                  ) : item.errors ? (
                    <span className="draft-problem" role="img" aria-label="Could not be accepted">
                      <WarningIcon />
                    </span>
                  ) : null}
                </span>
                <span className="draft-preview">{label}</span>
              </button>

              {item.status === 'pending' && (
                <span className="draft-actions">
                  <button type="button" className="icon-button" aria-label={`Edit draft: ${label}`} onClick={() => onEdit(item.id)}>
                    <PencilIcon />
                  </button>
                  <button
                    type="button"
                    className="icon-button icon-danger"
                    aria-label={`Discard draft: ${label}`}
                    onClick={() => onDiscard(item.id)}
                  >
                    <CrossIcon />
                  </button>
                </span>
              )}
            </li>
          )
        })}
      </ul>

      {placeholders === 0 && items.length === 0 && (
        <p className="draft-empty">No drafts yet. Fill in the form and generate some questions.</p>
      )}

      <footer className="draft-panel-footer">
        <button type="button" className="primary" disabled={pending === 0 || saving || run.phase === 'running'} onClick={onAcceptAll}>
          Accept all ({pending})
        </button>
      </footer>
    </aside>
  )
}
