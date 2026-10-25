// Small inline icons: no icon library is needed for four glyphs.
const props = {
  width: 16,
  height: 16,
  viewBox: '0 0 16 16',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
  'aria-hidden': true,
  focusable: false,
} as const

export const PencilIcon = () => (
  <svg {...props}>
    <path d="M11 2.5l2.5 2.5L5.5 13H3v-2.5z" />
  </svg>
)

export const CrossIcon = () => (
  <svg {...props}>
    <path d="M3.5 3.5l9 9M12.5 3.5l-9 9" />
  </svg>
)

export const CheckIcon = () => (
  <svg {...props}>
    <path d="M3 8.5l3.2 3.2L13 4.8" />
  </svg>
)

export const WarningIcon = () => (
  <svg {...props}>
    <path d="M8 3v6M8 12.2v.1" />
  </svg>
)

export const Spinner = () => <span className="spinner" aria-hidden="true" />
