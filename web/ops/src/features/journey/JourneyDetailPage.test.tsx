import { screen, within } from '@testing-library/react'
import { Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../core/api/client'
import type { ControllerJourneyDetail, DetailStop } from '../../core/api/types'
import { renderWithApi } from '../../test/helpers'
import { JourneyDetailPage } from './JourneyDetailPage'

const stop = (n: number, name: string, state: string, extra: Partial<DetailStop> = {}): DetailStop => ({
  serviceStopId: `s${n}`,
  sequence: n,
  stationCode: name.slice(0, 3).toUpperCase(),
  stationName: name,
  scheduledArrival: n === 1 ? null : `2026-10-15T0${n}:00:00Z`,
  scheduledDeparture: n === 3 ? null : `2026-10-15T0${n}:01:00Z`,
  estimatedArrival: null,
  estimatedDeparture: null,
  actualArrival: null,
  actualDeparture: null,
  confirmation: 'Pending',
  state,
  plannedPlatform: '1',
  platform: null,
  ...extra,
})

const detail = (overrides: Partial<ControllerJourneyDetail> = {}): ControllerJourneyDetail => ({
  journeyId: 'j1',
  trainNumber: '8058',
  operatingDate: '2026-10-15',
  direction: 'Inbound',
  category: 'Regional',
  serviceType: 'Slow',
  status: 'Running',
  statusKey: 'Delayed',
  statusGroup: 'Warning',
  delayMinutes: 6,
  delayKnown: true,
  serverTime: '2026-10-15T03:10:00Z',
  reportedReason: 'SignalIssue',
  verifiedReason: null,
  position: { stationCode: 'VEY', stationName: 'Veyangoda', state: 'Departed', at: '2026-10-15T02:51:00Z' },
  operator: { name: 'Nimal Perera', validFrom: '2026-10-15T02:20:00Z', validTo: null },
  stops: [
    stop(1, 'Veyangoda', 'Passed', { actualDeparture: '2026-10-15T02:51:00Z' }),
    stop(2, 'Gampaha', 'Next', { estimatedArrival: '2026-10-15T03:12:00Z' }),
    stop(3, 'Colombo Fort', 'Upcoming'),
  ],
  events: [
    { id: 'e2', at: '2026-10-15T03:06:00Z', type: 'DelayReported', source: 'Operator', actor: 'Nimal Perera', stationName: null, totalDelayMinutes: 6, delayUnknown: false, reason: 'SignalIssue', reasonCode: null },
    { id: 'e1', at: '2026-10-15T02:51:00Z', type: 'Departed', source: 'Operator', actor: 'Nimal Perera', stationName: 'Veyangoda', totalDelayMinutes: null, delayUnknown: false, reason: null, reasonCode: null },
  ],
  ...overrides,
})

function renderPage(answer: ControllerJourneyDetail | ApiError) {
  const get = vi.fn(async () => {
    if (answer instanceof ApiError) throw answer
    return answer
  })
  const api = { get: get as unknown as Api['get'], post: vi.fn() } satisfies Api
  return renderWithApi(
    <Routes>
      <Route path="/journeys/:journeyId" element={<JourneyDetailPage />} />
    </Routes>,
    api,
    '/journeys/j1',
  )
}

describe('Controller journey detail (spec 16 F1)', () => {
  it('shows the train, status, stops with their state, and where the train is in words', async () => {
    renderPage(detail())

    expect(await screen.findByRole('heading', { name: /Train 8058 · Veyangoda → Colombo Fort/ })).toBeInTheDocument()
    expect(screen.getByText('Delayed 6 min')).toBeInTheDocument()
    const rows = screen.getAllByRole('row')
    expect(within(rows[1]!).getByText('Passed')).toBeInTheDocument()
    expect(within(rows[2]!).getByText('Next')).toBeInTheDocument()
    expect(within(rows[2]!).getByText(/\(est\.\)/)).toBeInTheDocument()
    // The one-track view has a text alternative, and the same facts in a sentence.
    expect(screen.getByRole('img', { name: 'Position of train 8058 along its stops' })).toBeInTheDocument()
    expect(screen.getByText(/Departed Veyangoda at .* on the way to Gampaha/)).toBeInTheDocument()
  })

  it('lists the events newest first with who made them, and the delay reason the operator reported', async () => {
    renderPage(detail())

    const items = (await screen.findAllByRole('listitem')).map((li) => li.textContent ?? '')
    expect(items[0]).toContain('reported delay, 6 min, reason “Signal issue”')
    expect(items[0]).toContain('(Nimal Perera)')
    expect(items[1]).toContain('reported departed Veyangoda')
    expect(screen.getByText('Reported by the operator: Signal issue')).toBeInTheDocument()
    expect(screen.getByText(/Passengers do not see the reason until it is verified/)).toBeInTheDocument()
  })

  it('says what passengers see once the reason is verified', async () => {
    renderPage(detail({ verifiedReason: { code: 'SignalIssue', verifiedAt: '2026-10-15T03:09:00Z' } }))

    expect(await screen.findByText(/Passengers see “Signal issue”, verified by Operations at/)).toBeInTheDocument()
    expect(screen.getByText(/^Verified \d\d:\d\d$/)).toBeInTheDocument()
  })

  it('shows the operator and the crew links, or says no operator is assigned', async () => {
    const { unmount } = renderPage(detail())
    expect(await screen.findByText('Nimal Perera', { selector: 'div' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Crew history and end assignment' })).toHaveAttribute('href', '/journeys/j1/crew')
    unmount()

    renderPage(detail({ operator: null }))
    expect(await screen.findByText('No operator assigned')).toBeInTheDocument()
  })

  it('offers no action that changes a train, and says it does not predict', async () => {
    renderPage(detail())

    await screen.findByRole('heading', { name: /Train 8058/ })
    expect(screen.queryByRole('button', { name: /suspend|cancel|terminate|platform/i })).not.toBeInTheDocument()
    expect(screen.getByText(/does not control any train and does not predict/)).toBeInTheDocument()
  })

  it('says so when the journey does not exist', async () => {
    renderPage(new ApiError(404, 'journey_not_found'))

    expect(await screen.findByText('This journey was not found.')).toBeInTheDocument()
  })
})
