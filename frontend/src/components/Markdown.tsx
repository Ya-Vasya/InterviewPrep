import ReactMarkdown from 'react-markdown'
import rehypeHighlight from 'rehype-highlight'

/** Renders reference answers: markdown with syntax-highlighted fenced code (C#, SQL, ...). */
export function Markdown({ children }: { children: string }) {
  return (
    <div className="markdown">
      <ReactMarkdown rehypePlugins={[[rehypeHighlight, { detect: false }]]}>{children}</ReactMarkdown>
    </div>
  )
}
