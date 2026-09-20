import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = path.resolve(import.meta.dirname, '..')
const design = fs.readFileSync(path.resolve(root, '..', '.design/Frontend/DESIGN_SYSTEM.md'), 'utf8')
const css = fs.readFileSync(path.join(root, 'src/App.css'), 'utf8')
const app = fs.readFileSync(path.join(root, 'src/App.tsx'), 'utf8')

test('uses the applicable Mintlify tokens and typography roles', () => {
  for (const token of ['#0a0a0a', '#00d4a4', '#00b48a', '#7cebcb', '#e5e5e5', '#ededed', '#d45656', '#b3b3b3']) {
    assert.match(design, new RegExp(token.replace('#', '\\#')))
  }
  for (const token of ['--primary:#0a0a0a', '--mint:#00d4a4', '--mint-deep:#00b48a', '--mint-soft:#7cebcb', '--hairline:#e5e5e5', '--error:#d45656']) {
    assert.match(css, new RegExp(token.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
  }
  assert.match(css, /font-family:Inter/)
  assert.match(css, /"Geist Mono",monospace/)
  assert.doesNotMatch(css, /DM Sans|Space Mono/)
})

test('implements applicable component variants rather than only their default appearance', () => {
  for (const selector of ['.primary:active', '.primary:disabled', '.secondary', '.balance-card', '.entry-form input:focus', '.entry-form select:focus', '.status-banner.success', '.error-state']) {
    assert.match(css, new RegExp(selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
  }
  assert.match(css, /border-radius:999px/)
  assert.match(css, /min-height:40px/)
  assert.match(css, /height:40px/)
})

test('renders each operational state without calling a stale or unavailable balance current', () => {
  for (const state of ["'loading'", "'empty'", "'error'", "'unauthorized'", "'current'", "'stale'", "'unavailable'"]) assert.match(app, new RegExp(state))
  assert.match(app, /Dados em atualização · stale/)
  assert.match(app, /Indisponível/)
  assert.match(app, /Acesso não autorizado/)
  assert.match(app, /freshness \|\| screenState/)
})

test('preserves the documented responsive and touch-target behavior', () => {
  assert.match(css, /@media\(max-width:720px\)/)
  assert.match(css, /@media\(max-width:480px\)/)
  assert.match(css, /min-height:44px/)
  assert.match(css, /width:44px;height:44px/)
})
