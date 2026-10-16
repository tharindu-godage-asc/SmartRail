import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router'
import type { Api } from '../core/api/client'
import type { ClosedException, ClosedExceptions, ExceptionItem, OpenExceptions, Overview, OverviewJourney } from '../core/api/types'
import { ApiContext } from '../features/auth/apiContext'

/** A token the app can read the roles from. The signature is not checked in the browser (the API does that). */
export function makeToken(claims: Record<string, unknown>): string {
  const part = (value: unknown) => btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${part({ alg: 'RS256' })}.${part(claims)}.signature`
}

export const exception = (overrides: Partial<ExceptionItem> = {}): ExceptionItem => ({
  id: 'e1',
  journeyId: 'j1',
  trainNumber: '8058',
  stationName: 'Gampaha',
  type: 'ReasonAwaitingVerification',
  severity: 'Critical',
  createdAt: '2026-10-15T03:06:00Z',
  ageSeconds: 180,
  relatedEventId: 'ev1',
  ...overrides,
})

export const closedException = (overrides: Partial<ClosedException> = {}): ClosedException => ({
  id: 'c1',
  journeyId: 'j1',
  trainNumber: '8058',
  stationName: 'Gampaha',
  type: 'ReasonAwaitingVerification',
  severity: 'Critical',
  createdAt: '2026-10-15T09:22:00Z',
  resolvedAt: '2026-10-15T11:11:00Z',
  resolvedByName: 'jayasinghe',
  resolution: 'Acknowledged',
  secondsToResolve: 6540,
  ...overrides,
})

export const closedList = (items: ClosedException[], total = items.length): ClosedExceptions => ({ items, total, limit: 50 })

export const journey = (overrides: Partial<OverviewJourney> = {}): OverviewJourney => ({
  journeyId: 'j1',
  trainNumber: '8058',
  from: 'Veyangoda',
  to: 'Colombo Fort',
  statusGroup: 'Warning',
  statusKey: 'Delayed',
  delayMinutes: 15,
  delayKnown: true,
  lastEvent: { type: 'DelayReported', stationName: null, occurredAt: '2026-10-15T03:05:00Z', source: 'OperatorReport' },
  nextStop: { stationName: 'Gampaha', expectedAt: '2026-10-15T03:15:00Z' },
  secondsSinceUpdate: 90,
  verification: 'awaiting',
  ...overrides,
})

export const queue = (items: ExceptionItem[]): OpenExceptions => ({ items, openCount: items.length })
export const overview = (journeys: OverviewJourney[], openExceptionCount = 0): Overview => ({
  openExceptionCount,
  journeys,
  serverTime: '2026-10-15T03:08:00Z',
})

/** Renders a screen with a fake API and a fresh query cache. */
export function renderWithApi(ui: ReactElement, api: Api, route = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const result = render(
    <ApiContext.Provider value={{ api, blocked: null }}>
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </QueryClientProvider>
    </ApiContext.Provider>,
  )
  return { ...result, client }
}
