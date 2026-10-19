import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../core/api/client'
import { renderWithApi } from '../../test/helpers'
import { CancelledTab } from './CancelledTab'
import { ExtraTab } from './ExtraTab'
import { cancellation, detail, fakeApi, row } from './fixtures'
import { ServiceDetailPage } from './ServiceDetailPage'
import { TimetablePage } from './TimetablePage'
import { WeeklyTab } from './WeeklyTab'

const screens = (
  <Routes>
    <Route path="timetable" element={<TimetablePage />}>
      <Route index element={<WeeklyTab />} />
      <Route path="extra" element={<ExtraTab />} />
      <Route path="cancelled" element={<CancelledTab />} />
    </Route>
    <Route path="timetable/:serviceId" element={<ServiceDetailPage />} />
  </Routes>
)

describe('Timetable (spec 15, W1)', () => {
  it('lists the weekly services with route, days, first departure, stops, version and an Amended chip', async () => {
    const api = fakeApi({
      weekly: [
        row(),
        row({ serviceId: 's2', trainNumber: '8062', serviceType: 'Express', stopCount: 4, stopsLabel: 'Selected stops (4 of 5)', firstDeparture: '07:10' }),
        row({ serviceId: 's3', trainNumber: '8052', version: 3, amended: true, runsOn: ['Sat', 'Sun'] }),
      ],
    })
    renderWithApi(screens, api, '/timetable')

    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(4) // header + three
    const first = within(rows[1]!)
    expect(first.getByRole('link', { name: '8058' })).toHaveAttribute('href', '/timetable/s1')
    expect(first.getByText('Regional')).toBeInTheDocument()
    expect(first.getByText('Veyangoda to Colombo Fort')).toBeInTheDocument()
    expect(first.getByRole('img', { name: 'Runs on Mon, Tue, Wed, Thu, Fri' })).toBeInTheDocument()
    expect(first.getByText('08:20')).toBeInTheDocument()
    expect(first.getByText('Every stop (5)')).toBeInTheDocument()
    expect(first.getByText('Version 1')).toBeInTheDocument()
    expect(within(rows[2]!).getByText('Selected stops (4 of 5)')).toBeInTheDocument()
    expect(within(rows[2]!).getByText(/express/)).toBeInTheDocument()
    const amended = within(rows[3]!)
    expect(amended.getByText('Version 3')).toBeInTheDocument()
    expect(amended.getByText('Amended')).toBeInTheDocument() // the word beside the icon, not colour
    expect(amended.getByRole('img', { name: 'Runs on Sat, Sun' })).toBeInTheDocument()
  })

  it('shows the tab counts and the two actions', async () => {
    const api = fakeApi({ weekly: [row(), row({ serviceId: 's2' })], extra: [row({ serviceId: 'x1', date: '2026-10-22' })], cancelled: [] })
    renderWithApi(screens, api, '/timetable')

    expect(await screen.findByLabelText('2 Weekly schedule')).toBeInTheDocument()
    expect(await screen.findByLabelText('1 Extra trains')).toBeInTheDocument()
    expect(await screen.findByLabelText('0 Cancelled')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'New weekly service' })).toHaveAttribute('href', '/timetable/new')
    expect(screen.getByRole('link', { name: 'Add extra train' })).toHaveAttribute('href', '/timetable/extra/new')
  })

  it('shows the extra trains with their date, and an empty state that says what an extra train is', async () => {
    const user = userEvent.setup()
    renderWithApi(screens, fakeApi({ extra: [row({ serviceId: 'x1', trainNumber: '8099', date: '2026-10-22' })] }), '/timetable')

    await user.click(await screen.findByRole('link', { name: /Extra trains/ }))

    expect(await screen.findByText('Thu 22 Oct')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '8099' })).toBeInTheDocument()
  })

  it('says so when there are no extra trains, and when nothing is cancelled', async () => {
    const user = userEvent.setup()
    renderWithApi(screens, fakeApi(), '/timetable/extra')
    expect(await screen.findByText(/There are no extra trains/)).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: /Cancelled/ }))
    expect(await screen.findByText('No service is cancelled for today or later.')).toBeInTheDocument()
  })

  it('lists the cancelled services with the range, the reason as text, who and when', async () => {
    const api = fakeApi({
      cancelled: [
        cancellation(),
        cancellation({ cancellationId: 'c2', trainNumber: '8075', from: '2026-10-26', to: null, reason: '<b>not bold</b>' }),
      ],
    })
    renderWithApi(screens, api, '/timetable/cancelled')

    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3)
    const one = within(rows[1]!)
    expect(one.getByText('8072')).toBeInTheDocument()
    expect(one.getByText('Fri 23 Oct')).toBeInTheDocument()
    expect(one.getByText('Rolling stock not available')).toBeInTheDocument()
    expect(one.getByText('jayasinghe')).toBeInTheDocument()
    const onward = within(rows[2]!)
    expect(onward.getByText('Mon 26 Oct onward')).toBeInTheDocument()
    expect(onward.getByText('<b>not bold</b>')).toBeInTheDocument() // markup is shown as text
    expect(document.querySelector('td b')).toBeNull()
  })

  it('says it could not load and offers Try again, then loads', async () => {
    const user = userEvent.setup()
    const get = vi.fn().mockRejectedValueOnce(new ApiError(500, null)).mockResolvedValue([row()])
    renderWithApi(screens, { get: get as unknown as Api['get'], post: vi.fn() }, '/timetable/extra')

    expect(await screen.findByText('Could not load the timetable. Try again.')).toBeInTheDocument()
    await user.click(screen.getAllByRole('button', { name: 'Try again' })[0]!)

    expect(await screen.findByRole('link', { name: '8058' })).toBeInTheDocument()
  })

  it('shows a service: stops with platforms and Sinhala names, versions with the summary, cancellations, and the notes', async () => {
    const api = fakeApi({ detail: detail({ cancellations: [cancellation({ trainNumber: '8072' })] }) })
    renderWithApi(screens, api, '/timetable/s1')

    expect(await screen.findByRole('heading', { level: 1, name: /Timetable · 8072/ })).toBeInTheDocument()
    expect(screen.getByText('Version 2', { selector: 'span.font-semibold' })).toBeInTheDocument()
    expect(screen.getByText('වේයන්ගොඩ')).toHaveAttribute('lang', 'si')
    expect(screen.getByText('Platform 1')).toBeInTheDocument()
    expect(screen.getByText('Not set')).toBeInTheDocument()
    expect(screen.getByText('First stop')).toBeInTheDocument()
    expect(screen.getByText('Last stop')).toBeInTheDocument()
    expect(screen.getByText(/Veyangoda 17:30 \(was 17:25\)/)).toBeInTheDocument()
    expect(screen.getByText(/First version/)).toBeInTheDocument()
    expect(screen.getByText('Rolling stock not available')).toBeInTheDocument()
    expect(screen.getByText(/never deleted/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Back to the timetable/ })).toHaveAttribute('href', '/timetable')
    // No button that goes nowhere: cancel and amend are not offered yet, and nothing can be deleted.
    expect(screen.queryByRole('button')).toBeNull()
    expect(screen.queryByText(/delete/i, { selector: 'button, a' })).toBeNull()
  })

  it('says the service was not found for a 404', async () => {
    const get = vi.fn().mockRejectedValue(new ApiError(404, 'service_not_found'))
    renderWithApi(screens, { get: get as unknown as Api['get'], post: vi.fn() }, '/timetable/nope')

    expect(await screen.findByText('This service was not found.')).toBeInTheDocument()
  })
})
