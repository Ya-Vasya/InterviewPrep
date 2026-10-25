import { useState } from 'react'
import { Link } from 'react-router-dom'
import { TOPIC_NAMES, type Difficulty } from '../../api/client'
import { Markdown } from '../../components/Markdown'
import { AiGeneratedLabel } from '../ai/AiGeneratedLabel'
import type { DraftItem } from './draftsReducer'
import type { Draft } from '../../api/client'

const DIFFICULTIES: readonly Difficulty[] = ['Junior', 'Middle', 'Senior']

interface Props {
  item: DraftItem
  editing: boolean
  saving: boolean
  onEditingChange: (editing: boolean) => void
  onChange: (patch: Partial<Draft>) => void
  onAccept: () => void
  onDiscard: () => void
}

/** The selected Draft: read-only by default, editable with the pencil. Edits apply as you type, so nothing is lost by accepting. */
export function DraftDetail({ item, editing, saving, onEditingChange, onChange, onAccept, onDiscard }: Props) {
  const { draft } = item
  const pending = item.status === 'pending'

  return (
    <article className="draft-detail" aria-label="Selected draft">
      <header className="draft-detail-meta">
        <span>{TOPIC_NAMES[draft.topic]}</span>
        <span className={`level level-${draft.difficulty.toLowerCase()}`}>{draft.difficulty}</span>
        <AiGeneratedLabel />
      </header>

      {item.errors && (
        <ul className="error draft-errors" role="alert">
          {Object.entries(item.errors).flatMap(([field, messages]) =>
            messages.map((message) => <li key={`${field}-${message}`}>{message}</li>),
          )}
        </ul>
      )}

      {editing && pending ? (
        <DraftEditor key={item.id} draft={draft} onChange={onChange} />
      ) : (
        <>
          <p className="question">{draft.text}</p>
          <Markdown>{draft.answer}</Markdown>
          {draft.tags.length > 0 && (
            <p className="draft-tags">
              {draft.tags.map((tag) => (
                <span key={tag} className="draft-tag">
                  {tag}
                </span>
              ))}
            </p>
          )}
        </>
      )}

      {pending ? (
        <footer className="draft-detail-footer">
          <button type="button" className="primary" disabled={saving} onClick={onAccept}>
            Accept
          </button>
          <button type="button" onClick={() => onEditingChange(!editing)}>
            {editing ? 'Done editing' : 'Edit'}
          </button>
          <button type="button" className="danger" onClick={onDiscard}>
            Discard
          </button>
        </footer>
      ) : (
        <p className="draft-accepted-note">
          Saved to your questions. <Link to="/questions">View questions</Link>
        </p>
      )}
    </article>
  )
}

function DraftEditor({ draft, onChange }: { draft: Draft; onChange: (patch: Partial<Draft>) => void }) {
  // Kept as typed so a trailing comma survives; the Draft always holds the parsed tags.
  const [tagsText, setTagsText] = useState(draft.tags.join(', '))

  return (
    <div className="draft-editor">
      <label>
        Question
        <textarea rows={2} value={draft.text} onChange={(e) => onChange({ text: e.target.value })} />
      </label>

      <label>
        Answer (markdown)
        <textarea rows={14} value={draft.answer} onChange={(e) => onChange({ answer: e.target.value })} />
      </label>

      <label>
        Tags (comma separated)
        <input
          value={tagsText}
          onChange={(e) => {
            setTagsText(e.target.value)
            onChange({ tags: e.target.value.split(',').map((t) => t.trim()).filter((t) => t.length > 0) })
          }}
        />
      </label>

      <label>
        Difficulty
        <select value={draft.difficulty} onChange={(e) => onChange({ difficulty: e.target.value as Difficulty })}>
          {DIFFICULTIES.map((d) => (
            <option key={d} value={d}>
              {d}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}
