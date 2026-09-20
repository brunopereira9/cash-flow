import { defineConfig } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const directory = path.dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  testDir: './tests',
  testMatch: '**/*.e2e.spec.ts',
  workers: 1,
  timeout: 30_000,
  use: { baseURL: 'http://127.0.0.1:5174', browserName: 'chromium', headless: true },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5174',
    cwd: directory,
    url: 'http://127.0.0.1:5174/',
    timeout: 120_000,
    reuseExistingServer: !process.env.CI,
    env: {
      ...process.env,
      VITE_OIDC_AUTHORITY: 'http://127.0.0.1:4179/realms/test',
      VITE_CORE_URL: 'http://127.0.0.1:5080',
      VITE_SUMMARY_URL: 'http://127.0.0.1:5081',
    },
  },
})
