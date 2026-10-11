import ReactMarkdown from 'react-markdown'
import { rehypeHighlightLite } from './rehypeHighlightLite'

/** Renders reference answers: markdown with syntax-highlighted fenced code (C#, SQL, ...). */
export function Markdown({ children }: { children: string }) {
  return (
    <div className="markdown">
      <ReactMarkdown rehypePlugins={[rehypeHighlightLite]}>{children}</ReactMarkdown>
    </div>
  )
}
