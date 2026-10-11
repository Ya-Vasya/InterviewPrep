import type { Element, ElementContent, Root } from 'hast'
import bash from 'highlight.js/lib/languages/bash'
import csharp from 'highlight.js/lib/languages/csharp'
import javascript from 'highlight.js/lib/languages/javascript'
import json from 'highlight.js/lib/languages/json'
import sql from 'highlight.js/lib/languages/sql'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'
import yaml from 'highlight.js/lib/languages/yaml'
import { createLowlight } from 'lowlight'

// Only the languages interview answers use. rehype-highlight always bundles lowlight's 37 default
// languages (about 400 kB), so this plugin uses lowlight directly with a small registry. Aliases
// (cs, c#, html, sh, ts, ...) come with each language definition. Other fences stay unhighlighted.
const lowlight = createLowlight({ bash, csharp, javascript, json, sql, typescript, xml, yaml })

const textOf = (node: ElementContent): string =>
  node.type === 'text' ? node.value : node.type === 'element' ? node.children.map(textOf).join('') : ''

const languageOf = (code: Element): string | undefined =>
  (Array.isArray(code.properties.className) ? code.properties.className : [])
    .map(String)
    .find((c) => c.startsWith('language-'))
    ?.slice('language-'.length)

function highlightBlock(pre: Element) {
  const code = pre.children.find((c): c is Element => c.type === 'element' && c.tagName === 'code')
  const language = code && languageOf(code)
  if (!code || !language || !lowlight.registered(language)) return

  code.children = lowlight.highlight(language, textOf(code)).children as ElementContent[]
  code.properties.className = ['hljs', ...(code.properties.className as string[])]
}

function walk(node: Root | Element) {
  for (const child of node.children) {
    if (child.type !== 'element') continue
    if (child.tagName === 'pre') highlightBlock(child)
    else walk(child)
  }
}

/** rehype plugin: syntax-highlights fenced code blocks that name a registered language. */
export function rehypeHighlightLite() {
  return (tree: Root) => walk(tree)
}
