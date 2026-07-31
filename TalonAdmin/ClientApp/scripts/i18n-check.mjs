#!/usr/bin/env node
/**
 * Checks the translation catalogues against each other and against the source.
 *
 * Three things go wrong with a catalogue over time and none of them break the build: a key gets
 * used that nobody translated, a key gets deleted from the source but left in every catalogue, and
 * a new English string gets added without the other languages following. This reports all three.
 *
 * It also lists user-facing text that never made it into a catalogue at all — heuristically, since
 * there is no way to be certain a capitalised string in a template is meant for a human. Treat that
 * list as a prompt, not a verdict.
 *
 *   npm run i18n:check
 */
import { readFileSync, readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join, relative } from 'node:path'
import { globSync } from 'node:fs'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const localeDir = join(root, 'src/i18n')

const REFERENCE = 'en'

/** Flattens { a: { b: 'x' } } to { 'a.b': 'x' } so catalogues compare key for key. */
function flatten(value, prefix = '') {
  return Object.entries(value).flatMap(([key, entry]) => {
    const path = prefix ? `${prefix}.${key}` : key
    return typeof entry === 'object' && entry !== null
      ? flatten(entry, path)
      : [[path, entry]]
  })
}

const catalogues = Object.fromEntries(
  readdirSync(localeDir)
    .filter(f => f.endsWith('.json'))
    .map(f => [f.replace('.json', ''), new Map(flatten(JSON.parse(readFileSync(join(localeDir, f), 'utf8'))))])
)

const reference = catalogues[REFERENCE]
if (!reference) {
  console.error(`No ${REFERENCE}.json — there is nothing to compare against.`)
  process.exit(1)
}

const sources = globSync('src/**/*.{vue,ts}', { cwd: root })
  .filter(f => !f.includes('/i18n/'))
  .map(f => ({ file: f, text: readFileSync(join(root, f), 'utf8') }))

// t('a.b'), t("a.b"), $t('a.b') and the descriptor fields that hold bare keys.
const KEY_PATTERN = /(?<![\w$.])\$?t\(\s*['"`]([\w.]+)['"`]/g
const FIELD_PATTERN = /(?:label|title|singular|help|emptyMessage):\s*'([a-z][\w]*\.[\w.]+)'/g

const used = new Set()
for (const { text } of sources) {
  for (const m of text.matchAll(KEY_PATTERN)) used.add(m[1])
  for (const m of text.matchAll(FIELD_PATTERN)) used.add(m[1])
}

// Keys reached through a variable — t(spec.description), or a route's meta.title — are invisible
// to the patterns above. Any key whose exact text appears as a string literal anywhere in the
// source counts as used; a name that specific is not a coincidence.
for (const key of reference.keys()) {
  const quoted = [`'${key}'`, `"${key}"`, `\`${key}\``]
  if (sources.some(({ text }) => quoted.some(q => text.includes(q)))) used.add(key)
}

// Dynamic keys — t(`messageState.${state}`) — cannot be resolved statically, so their whole
// namespace is treated as used rather than reported as dead.
const dynamicPrefixes = new Set()
for (const { text } of sources) {
  for (const m of text.matchAll(/(?<![\w$.])\$?t\(\s*`([\w.]+)\.\$\{/g)) dynamicPrefixes.add(m[1])
}

const problems = []

const missing = [...used].filter(k => !reference.has(k)).sort()
if (missing.length) {
  problems.push(`${missing.length} key(s) used in source but absent from ${REFERENCE}.json:`)
  problems.push(...missing.map(k => `    ${k}`))
}

const unused = [...reference.keys()]
  .filter(k => !used.has(k) && ![...dynamicPrefixes].some(p => k.startsWith(p + '.')))
  .sort()
if (unused.length) {
  problems.push(`${unused.length} key(s) in ${REFERENCE}.json that nothing uses:`)
  problems.push(...unused.map(k => `    ${k}`))
}

for (const [locale, catalogue] of Object.entries(catalogues)) {
  if (locale === REFERENCE) continue

  const untranslated = [...reference.keys()].filter(k => !catalogue.has(k)).sort()
  const orphaned = [...catalogue.keys()].filter(k => !reference.has(k)).sort()

  if (untranslated.length) {
    problems.push(`${locale}: ${untranslated.length} key(s) not translated (falls back to ${REFERENCE}):`)
    problems.push(...untranslated.slice(0, 20).map(k => `    ${k}`))
    if (untranslated.length > 20) problems.push(`    … and ${untranslated.length - 20} more`)
  }
  if (orphaned.length) {
    problems.push(`${locale}: ${orphaned.length} key(s) not in ${REFERENCE}.json:`)
    problems.push(...orphaned.map(k => `    ${k}`))
  }
}

// Heuristic: sentence-like text sitting directly in a template.
const LITERAL_PATTERN = />\s*([A-Z][A-Za-z][^<>{}\n]{3,70}?)\s*</g
const suspects = []
for (const { file, text } of sources) {
  if (!file.endsWith('.vue')) continue
  const template = text.slice(text.indexOf('<template>'))
  for (const m of template.matchAll(LITERAL_PATTERN)) {
    const literal = m[1].trim()
    if (/^[A-Z]{1,4}$/.test(literal)) continue
    suspects.push(`    ${relative('.', file)}: ${literal}`)
  }
}

if (problems.length) {
  console.error('Catalogue problems\n')
  console.error(problems.join('\n'))
}

if (suspects.length) {
  console.error(`\n${suspects.length} literal(s) in templates that may need translating:\n`)
  console.error(suspects.join('\n'))
}

if (!problems.length && !suspects.length) {
  console.log(`Catalogues agree, and no untranslated literals found. ${reference.size} keys.`)
}

// Only genuine catalogue problems fail the check; the literal list is advisory, because deciding
// what is user-facing needs a human.
process.exit(problems.length ? 1 : 0)
