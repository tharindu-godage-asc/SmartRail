import { defineConfig } from '@playwright/test'

/**
 * End-to-end test of spec 08 against the Docker Compose stack (API 8080, Keycloak 8180). Run it by hand, not in CI (same position as
 * scripts/smoke-auth.sh): start Compose first with `docker compose up --build` in smartrail-platform, then `npm run e2e` here.
 * The dev server on port 5173 is started for the test if it is not already running.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  workers: 1,
  retries: 0,
  // On CI (and for the QA script) an HTML report is written as well, so a failure comes with its trace to open.
  reporter: process.env.CI || process.env.QA ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: { baseURL: 'http://localhost:5173', trace: 'retain-on-failure' },
  // DISPLAY_ONLY=1 skips the dashboard's dev server: the station displays are their own service and must work without it.
  webServer: process.env.DISPLAY_ONLY
    ? undefined
    : { command: 'npm run dev', url: 'http://localhost:5173', reuseExistingServer: true, timeout: 60_000 },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
})
