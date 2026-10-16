// Turns smartrail-tokens.json (DTCG, ADR-16) into CSS for the web app: plain variables, then a Tailwind v4 theme with the same values under short names.
// Pure string work, so `npm run tokens` and the unit test call the same function.

const HEADER = `/* GENERATED FILE. Do not edit by hand.
 * Source: mobile/operator_app/smartrail-tokens.json (one source for the operator app and the web app, ADR-16).
 * Regenerate with: npm run tokens */
`

/** Every token with a $value, keyed by its path, e.g. ["color", "status", "normal", "fg"]. */
function leaves(node, path = []) {
  const found = []
  for (const [key, value] of Object.entries(node)) {
    if (key.startsWith('$') || typeof value !== 'object' || value === null) continue
    if ('$value' in value) found.push({ path: [...path, key], token: value })
    else found.push(...leaves(value, [...path, key]))
  }
  return found
}

const kebab = (word) => word.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase()
const variableName = (path) => `--${path.map(kebab).join('-')}`

function cssValue(token) {
  const value = token.$value
  if (Array.isArray(value)) return value.map((name) => (/\s/.test(name) ? `'${name}'` : name)).join(', ')
  return String(value)
}

/** Which tokens become CSS variables: colours, sizes, type, spacing and so on. Icons and non-CSS entries are left out. */
function isCss(entry) {
  const group = entry.path[0]
  if (group === 'icon') return false
  if (entry.token.$type === 'other') return false
  if (group === 'font' && entry.path[1] === 'feature') return false
  return entry.token.$value !== undefined
}

// Short names for Tailwind utilities (bg-page, text-ink, border-line, p-md, rounded-lg ...). Each points at a token path.
const COLOR_ALIASES = {
  page: 'color.bg.page',
  surface: 'color.bg.surface',
  ink: 'color.text.primary',
  muted: 'color.text.secondary',
  line: 'color.border.default',
  normal: 'color.status.normal.fg',
  'normal-bg': 'color.status.normal.chipBg',
  warning: 'color.status.warning.fg',
  'warning-bg': 'color.status.warning.chipBg',
  unknown: 'color.status.unknown.fg',
  'unknown-bg': 'color.status.unknown.chipBg',
  disrupted: 'color.status.disrupted.fg',
  'disrupted-bg': 'color.status.disrupted.chipBg',
  'on-disrupted': 'color.status.disrupted.onFilled',
}
const SPACING = ['xs', 'sm', 'md', 'lg', 'xl']
const RADII = ['sm', 'md', 'lg', 'pill']
const TEXT_SIZES = { heading: 'mobile.heading', body: 'mobile.body', label: 'mobile.label', caption: 'mobile.caption' }

export function generate(tokens) {
  const entries = leaves(tokens).filter(isCss)
  const byPath = new Map(entries.map((e) => [e.path.join('.'), e]))
  // The theme gets the token's literal value (not var(--x)): some names are the same in both places, and a self-reference would be invalid.
  const need = (path) => {
    const entry = byPath.get(path)
    if (!entry) throw new Error(`smartrail-tokens.json has no token "${path}"`)
    return cssValue(entry.token)
  }

  const root = entries.map((e) => `  ${variableName(e.path)}: ${cssValue(e.token)};`).join('\n')

  const theme = [
    ...Object.entries(COLOR_ALIASES).map(([name, path]) => `  --color-${name}: ${need(path)};`),
    ...SPACING.map((name) => `  --spacing-${name}: ${need(`space.${name}`)};`),
    ...RADII.map((name) => `  --radius-${name}: ${need(`radius.${name}`)};`),
    `  --font-sans: ${need('font.family.latin')};`,
    ...Object.entries(TEXT_SIZES).map(([name, path]) => `  --text-${name}: ${need(`type.${path}.size`)};`),
  ].join('\n')

  return `${HEADER}
:root {
${root}
}

@theme {
${theme}
}

@utility min-h-touch {
  min-height: ${need('size.touchTarget.min')};
}

@utility min-w-touch {
  min-width: ${need('size.touchTarget.min')};
}
`
}
