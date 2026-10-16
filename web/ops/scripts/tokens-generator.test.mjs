import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { generate } from './tokens-generator.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const tokens = JSON.parse(readFileSync(resolve(here, '../../../mobile/operator_app/smartrail-tokens.json'), 'utf8'))

describe('token generator', () => {
  it('matches the committed src/styles/tokens.css (run `npm run tokens` after changing the token file)', () => {
    expect(readFileSync(resolve(here, '../src/styles/tokens.css'), 'utf8')).toBe(generate(tokens))
  })

  it('writes the design variables with the names the canvas uses', () => {
    const css = generate(tokens)
    expect(css).toContain('--color-bg-page: #FFFFFF;')
    expect(css).toContain('--color-status-warning-chip-bg: #F3E9E0;')
    expect(css).toContain('--status-chip-warning-outline-width: 3px;')
    expect(css).toContain("--font-family-latin: 'Noto Sans', sans-serif;")
    expect(css).toContain('--size-touch-target-min: 44px;')
  })

  it('gives Tailwind short names with the literal token values, never a variable that points at itself', () => {
    const css = generate(tokens)
    expect(css).toContain('--color-page: #FFFFFF;')
    expect(css).toContain('--color-disrupted: #B0003A;')
    expect(css).toContain('--spacing-md: 16px;')
    expect(css).toContain('--radius-lg: 12px;')
    expect(css).not.toMatch(/--radius-md: var\(--radius-md\)/)
    expect(css).toContain('min-height: 44px;')
  })

  it('leaves out the dark values and the icon names', () => {
    const css = generate(tokens)
    expect(css).not.toContain('#262B32')
    expect(css).not.toContain('pause_circle')
  })

  it('stops with a clear message when a token the app needs is missing', () => {
    const broken = structuredClone(tokens)
    delete broken.space.md
    expect(() => generate(broken)).toThrow('no token "space.md"')
  })
})
