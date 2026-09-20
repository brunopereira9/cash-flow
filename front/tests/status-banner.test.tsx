import assert from 'node:assert/strict'
import test from 'node:test'
import React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { StatusBanner } from '../src/components/StatusBanner.tsx'

test('renders the operational error state with an accessible alert role', () => {
  const markup = renderToStaticMarkup(<StatusBanner tone="error">Acesso não autorizado.</StatusBanner>)
  assert.match(markup, /role="alert"/)
  assert.match(markup, /Acesso não autorizado\./)
  assert.match(markup, /status-banner error/)
})

test('renders a success state without presenting it as an error', () => {
  const markup = renderToStaticMarkup(<StatusBanner tone="success">Lançamento salvo.</StatusBanner>)
  assert.match(markup, /role="status"/)
  assert.doesNotMatch(markup, /role="alert"/)
})
