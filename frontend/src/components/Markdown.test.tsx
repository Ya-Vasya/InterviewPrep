import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Markdown } from './Markdown'

const fence = (language: string, code: string) => `\`\`\`${language}\n${code}\n\`\`\``

describe('Markdown', () => {
  it('highlights C# code blocks', () => {
    const { container } = render(<Markdown>{fence('csharp', 'public async Task<int> RunAsync() => 42;')}</Markdown>)

    expect(container.querySelector('pre code.hljs')).toBeInTheDocument()
    expect(container.querySelector('.hljs-keyword')?.textContent).toBe('public')
    expect(container.querySelector('code')?.textContent?.trimEnd()).toBe('public async Task<int> RunAsync() => 42;')
  })

  it('highlights SQL code blocks', () => {
    const { container } = render(<Markdown>{fence('sql', "SELECT Name FROM Users WHERE Id = 1;")}</Markdown>)

    expect(container.querySelector('.hljs-keyword')).toBeInTheDocument()
  })

  it('understands language aliases such as cs', () => {
    const { container } = render(<Markdown>{fence('cs', 'var x = 1;')}</Markdown>)

    expect(container.querySelector('.hljs-keyword')?.textContent).toBe('var')
  })

  it('leaves blocks without a known language as plain text', () => {
    const unlabeled = render(<Markdown>{fence('', 'public class A {}')}</Markdown>)
    const unknown = render(<Markdown>{fence('cobol', 'MOVE A TO B.')}</Markdown>)

    expect(unlabeled.container.querySelector('[class*="hljs-"]')).toBeNull()
    expect(unknown.container.querySelector('[class*="hljs-"]')).toBeNull()
    expect(unknown.container.querySelector('code')?.textContent?.trimEnd()).toBe('MOVE A TO B.')
  })

  it('renders raw HTML in an answer as text, not markup', () => {
    const { container } = render(<Markdown>{'<img src=x onerror=alert(1)> hello'}</Markdown>)

    expect(container.querySelector('img')).toBeNull()
  })

  it('renders inline code and lists', () => {
    const { container } = render(<Markdown>{'Use `await`:\n\n- one\n- two'}</Markdown>)

    expect(container.querySelector('p code')?.textContent).toBe('await')
    expect(container.querySelectorAll('li')).toHaveLength(2)
  })
})
