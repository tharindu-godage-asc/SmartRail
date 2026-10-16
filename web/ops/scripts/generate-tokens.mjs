import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { generate } from './tokens-generator.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const source = resolve(here, '../../../mobile/operator_app/smartrail-tokens.json')
const target = resolve(here, '../src/styles/tokens.css')

writeFileSync(target, generate(JSON.parse(readFileSync(source, 'utf8'))))
console.log(`Wrote ${target}`)
