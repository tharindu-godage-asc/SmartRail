import { act, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Route, Routes } from 'react-router'
import { ApiError, type Api } from '../../core/api/client'
import { exception, journey, overview, queue, renderWithApi } from '../../test/helpers'
import { OpsShell } from './OpsShell'

vi.mock('react-oidc-context', () => ({
  useAuth: () => ({ user: { profile: { preferred_username: 'jayasinghe' } }, signoutRedirect: vi.fn() }),
}))

const shell = (
  <Routes>
    <Route element={<OpsShell />}>
      <Route index element={<p>content</p>} />
    </Route>
  </Routes>
)

beforeEach(() => {
  // Only the clock and intervals are faked: polling and the freshness tick use them; promises and the DOM stay real.
  vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
  vi.setSystemTime(new Date('2026-10-15T08:32:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
})

const pass = (ms: number) => act(async () => { await vi.advanceTimersByTimeAsync(ms) })

describe('Operations shell', () => {
  it('always shows the safety notice, the open count in the menu, who is signed in, and "Connected"', async () => {
    const get = vi.fn(async (path: string) => (path.includes('overview') ? overview([journey()], 1) : queue([exception()])))
    renderWithApi(shell, { get: get as unknown as Api['get'], post: vi.fn() })

    expect(screen.getByText(/information system, not a safety system/)).toBeInTheDocument()
    expect(await screen.findByLabelText('1 open')).toBeInTheDocument()
    expect(screen.getByText(/jayasinghe · Controller/)).toBeInTheDocument()
    expect(screen.getByText('Connected')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /close|dismiss/i })).not.toBeInTheDocument() // the notice cannot be closed
  })

  it('shows "Not up to date" with the last good time after 30 seconds of failures, and keeps the count', async () => {
    const get = vi
      .fn()
      .mockResolvedValueOnce(queue([exception()]))
      .mockResolvedValueOnce(overview([journey()], 1))
      .mockRejectedValue(new ApiError(503, null))
    renderWithApi(shell, { get: get as unknown as Api['get'], post: vi.fn() })
    await screen.findByLabelText('1 open')
    expect(screen.queryByText(/Not up to date/)).not.toBeInTheDocument()

    await pass(31_000)

    const banner = await screen.findByText(/Not up to date\. Last updated 14:02:0\d/)
    expect(banner).toBeInTheDocument()
    expect(screen.getByLabelText('1 open')).toBeInTheDocument() // never cleared on an error
  })

  it('says nothing on the first load, then announces a new Critical exception once', async () => {
    const first = queue([exception({ id: 'e1' })])
    const second = queue([exception({ id: 'e1' }), exception({ id: 'e2', trainNumber: '8065' })])
    let calls = 0
    const get = vi.fn(async (path: string) => {
      if (path.includes('overview')) return overview([journey()], 1)
      calls += 1
      return calls === 1 ? first : second
    })
    renderWithApi(shell, { get: get as unknown as Api['get'], post: vi.fn() })
    await screen.findByLabelText('1 open')
    const liveRegion = () => document.querySelector('[aria-live="polite"]')!
    expect(liveRegion()).toHaveTextContent('')

    await pass(10_000)

    await waitFor(() => expect(liveRegion()).toHaveTextContent('New critical exception for train 8065'))
  })
})
