import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { vi } from 'vitest'
import App from '../../App'
import type { Board, BoardCall } from './types'

/** Test data for the station displays. 03:00 UTC is 08:30 in Sri Lanka. */
export const call = (overrides: Partial<BoardCall> = {}): BoardCall => ({
  trainNumber: '8058',
  serviceType: 'Slow',
  operatingDate: '2026-10-15',
  destination: { code: 'CF', name: { en: 'Colombo Fort', si: 'කොළඹ කොටුව', ta: 'கொழும்பு கோட்டை' } },
  platform: '1',
  live: true,
  journeyId: 'j1',
  arrival: { scheduled: '2026-10-15T03:00:00Z', estimated: '2026-10-15T03:08:00Z' },
  departure: { scheduled: '2026-10-15T03:01:00Z', estimated: '2026-10-15T03:09:00Z' },
  state: 'Due',
  delayMinutes: 8,
  delayKnown: true,
  statusGroup: 'Warning',
  statusKey: 'Delayed',
  version: 3,
  lastEventAt: '2026-10-15T02:57:00Z',
  lastSeen: null,
  ...overrides,
})

export const board = (overrides: Partial<Board> = {}): Board => ({
  station: { code: 'GPH', name: { en: 'Gampaha', si: 'ගම්පහ', ta: 'கம்பஹா' } },
  serverTime: '2026-10-15T02:58:00Z', // 08:28
  live: true,
  trains: [call()],
  next: null,
  firstTomorrow: null,
  ...overrides,
})

export const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

/** Renders the whole app at an address, with fetch replaced by `answer`. Returns the fetch mock so a test can look at the calls. */
export function renderDisplay(path: string, answer: (url: string) => Response | Promise<Response>) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    void init
    return answer(url)
  })
  vi.stubGlobal('fetch', fetchMock)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...view, fetchMock, client }
}
