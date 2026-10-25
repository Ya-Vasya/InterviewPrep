import './ai.css'

/** Reference answers written by the AI can be wrong; shown on every Draft and AI-sourced question. */
export function AiGeneratedLabel() {
  return <span className="ai-generated-label">AI-generated, verify before relying on it</span>
}
