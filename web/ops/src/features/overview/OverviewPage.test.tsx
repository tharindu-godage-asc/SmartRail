import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { Api } from '../../core/api/client'
import { exception, journey, overview, queue, renderWithApi } from '../../test/helpers'
import { OverviewPage } from './OverviewPage'

function fakeApi(journeys = [journey()], items = [exception()]) {
  const get = vi.fn(async (path: string) => (path.includes('overview') ? overview(journeys, items.length) : queue(items)))
  return { get: get as unknown as Api['get'], post: vi.fn() } satisfies Api
}

describe('Overview page', () => {
  it('shows the journey with route, next stop and time, status, last report with its source label, and verification (AC-E05-01 1, 3, 6)', async () => {
    renderWithApi(<OverviewPage />, fakeApi())

    const row = within((await screen.findByText('Veyangoda → Colombo Fort')).closest('tr')!)
    expect(row.getByText('8058')).toBeInTheDocument()
    expect(row.getByText('Gampaha')).toBeInTheDocument()
    expect(row.getByText('08:45')).toBeInTheDocument() // 03:15 UTC in Sri Lanka
    expect(row.getByText('Delayed 15 min')).toBeInTheDocument()
    expect(row.getByText('Delay reported')).toBeInTheDocument()
    expect(row.getByText(/Operator report/)).toBeInTheDocument() // the source in words
    expect(row.getByText(/1 min ago/)).toBeInTheDocument()
    expect(row.getByText('Awaiting verification')).toBeInTheDocument()
  })

  it('says "Delay unknown" with no number and no next time (spec 06 R7)', async () => {
    renderWithApi(<OverviewPage />, fakeApi([journey({ delayKnown: false, nextStop: null })]))

    const row = within((await screen.findByText('Veyangoda → Colombo Fort')).closest('tr')!)
    expect(row.getByText('Delay unknown')).toBeInTheDocument()
    expect(row.queryByText(/Delayed/)).not.toBeInTheDocument()
  })

  it('shows "No report yet" for a journey nobody has reported on, and a dash for no verification', async () => {
    renderWithApi(<OverviewPage />, fakeApi([journey({ lastEvent: null, nextStop: null, verification: 'none', statusKey: 'Scheduled', statusGroup: 'Normal', delayMinutes: 0 })]))

    const row = within((await screen.findByText('Veyangoda → Colombo Fort')).closest('tr')!)
    expect(row.getByText('No report yet')).toBeInTheDocument()
    expect(row.getByText('Scheduled')).toBeInTheDocument()
    expect(row.queryByText('Awaiting verification')).not.toBeInTheDocument()
  })

  it('shows the open count, critical count and awaiting count in the tiles (AC-E05-01 4)', async () => {
    renderWithApi(<OverviewPage />, fakeApi())

    const open = screen.getByText('Open exceptions', { selector: 'span' }).closest('div')!
    await waitFor(() => expect(within(open).getByText('1 critical')).toBeInTheDocument())
    expect(within(open).getByText('1')).toBeInTheDocument()
    const awaiting = screen.getByText('Awaiting verification', { selector: 'span.text-label' }).closest('div')!
    expect(within(awaiting).getByText('1')).toBeInTheDocument()
  })

  it('says "No trains running now" when nothing is in progress', async () => {
    renderWithApi(<OverviewPage />, fakeApi([], []))

    expect(await screen.findByText('No trains running now')).toBeInTheDocument()
  })

  it('lists the latest exceptions with a link to the queue', async () => {
    renderWithApi(<OverviewPage />, fakeApi())

    expect(await screen.findByRole('link', { name: 'View all exceptions' })).toHaveAttribute('href', '/exceptions')
  })
})
