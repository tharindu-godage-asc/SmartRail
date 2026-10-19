import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../core/api/client'
import { renderWithApi } from '../../test/helpers'
import { cancellation, detail, fakeApi, valid } from './fixtures'
import { ServiceDetailPage } from './ServiceDetailPage'
import { AmendServicePage } from './wizard/NewServicePage'

const pages = (
  <Routes>
    <Route path="timetable/:serviceId" element={<ServiceDetailPage />} />
    <Route path="timetable/:serviceId/amend" element={<AmendServicePage />} />
  </Routes>
)

/** A post that answers each path from a table: a value to return, or an Error to throw (the last answer repeats). */
function posts(answers: Record<string, unknown[]>) {
  const queues = Object.fromEntries(Object.entries(answers).map(([k, v]) => [k, [...v]]))
  return vi.fn(async (path: string) => {
    const key = Object.keys(queues).find((k) => path.endsWith(k))
    const queue = key ? queues[key]! : []
    const next = queue.length > 1 ? queue.shift() : queue[0]
    if (next instanceof Error) throw next
    return next
  }) as unknown as Api['post'] & ReturnType<typeof vi.fn>
}

const openDialog = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(await screen.findByRole('button', { name: 'Cancel service' }))
  return screen.getByRole('dialog')
}

describe('Cancel a service (spec 15 W3)', () => {
  it('asks first, needs a reason, shows the counter and the not-sent notice, and sends "this date only" as from = to', async () => {
    const user = userEvent.setup()
    const post = posts({ '/cancel': [{ cancellationId: 'c1', cancelledJourneys: 1, from: '2026-10-23', to: '2026-10-23' }] })
    renderWithApi(pages, fakeApi({}, post), '/timetable/s1')

    const dialog = await openDialog(user)
    expect(post).not.toHaveBeenCalled()
    expect(within(dialog).getByText(/Messages to passengers are not sent yet/)).toBeInTheDocument()
    const confirm = within(dialog).getByRole('button', { name: 'Confirm cancellation' })
    expect(confirm).toBeDisabled() // no reason yet
    expect(within(dialog).getByText('0 / 200')).toBeInTheDocument()

    fireEvent.change(within(dialog).getByLabelText('Date'), { target: { value: '2026-10-23' } })
    await user.type(within(dialog).getByLabelText('Reason'), 'Rolling stock not available')
    expect(within(dialog).getByText('27 / 200')).toBeInTheDocument()
    expect(confirm).toBeEnabled()
    await user.click(confirm)

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(post).toHaveBeenCalledWith('/api/v1/timetable/services/s1/cancel', { from: '2026-10-23', to: '2026-10-23', reason: 'Rolling stock not available' })
    expect(await screen.findByText('Cancelled 1 journey.')).toBeInTheDocument()
  })

  it('sends "from this date onward" with no end date and trims the reason', async () => {
    const user = userEvent.setup()
    const post = posts({ '/cancel': [{ cancellationId: 'c1', cancelledJourneys: 4, from: '2026-10-26', to: null }] })
    renderWithApi(pages, fakeApi({}, post), '/timetable/s1')

    const dialog = await openDialog(user)
    await user.click(within(dialog).getByRole('radio', { name: 'From this date onward' }))
    fireEvent.change(within(dialog).getByLabelText('Date'), { target: { value: '2026-10-26' } })
    await user.type(within(dialog).getByLabelText('Reason'), '  Strike  ')
    await user.click(within(dialog).getByRole('button', { name: 'Confirm cancellation' }))

    await screen.findByText('Cancelled 4 journeys.')
    expect(post).toHaveBeenCalledWith('/api/v1/timetable/services/s1/cancel', { from: '2026-10-26', to: null, reason: 'Strike' })
  })

  it('cannot go past 200 characters and sends one request on a double click', async () => {
    const user = userEvent.setup()
    let release: (value: unknown) => void = () => {}
    const post = vi.fn(() => new Promise((resolve) => (release = resolve))) as unknown as Api['post'] & ReturnType<typeof vi.fn>
    renderWithApi(pages, fakeApi({}, post), '/timetable/s1')

    const dialog = await openDialog(user)
    const reason = within(dialog).getByLabelText('Reason')
    expect(reason).toHaveAttribute('maxlength', '200')
    await user.type(reason, 'x')
    const confirm = within(dialog).getByRole('button', { name: 'Confirm cancellation' })
    await user.dblClick(confirm)

    expect(post).toHaveBeenCalledTimes(1)
    expect(within(dialog).getByRole('button', { name: 'Cancelling…' })).toBeDisabled()
    release({ cancellationId: 'c', cancelledJourneys: 1, from: 'x', to: null })
  })

  it('shows the refused state with the date when a journey has reports, changes nothing, and Try another date goes back to the form', async () => {
    const user = userEvent.setup()
    const refused = new ApiError(409, 'journey_has_events', { reason: 'journey_has_events', date: '2026-10-23' })
    renderWithApi(pages, fakeApi({}, posts({ '/cancel': [refused] })), '/timetable/s1')

    const dialog = await openDialog(user)
    await user.type(within(dialog).getByLabelText('Reason'), 'x')
    await user.click(within(dialog).getByRole('button', { name: 'Confirm cancellation' }))

    const alert = await within(dialog).findByRole('alert')
    expect(alert).toHaveTextContent('A journey on Fri 23 Oct already has reports. Nothing was changed.')
    expect(within(dialog).queryByRole('button', { name: 'Confirm cancellation' })).toBeNull()
    await user.click(within(dialog).getByRole('button', { name: 'Try another date' }))
    expect(within(dialog).getByLabelText('Reason')).toBeInTheDocument()
    expect(within(dialog).queryByRole('alert')).toBeNull()
  })

  it('words a 422 in plain language and keeps the form', async () => {
    const user = userEvent.setup()
    const refused = new ApiError(422, 'nothing_to_cancel', { reason: 'nothing_to_cancel' })
    renderWithApi(pages, fakeApi({}, posts({ '/cancel': [refused] })), '/timetable/s1')

    const dialog = await openDialog(user)
    await user.type(within(dialog).getByLabelText('Reason'), 'x')
    await user.click(within(dialog).getByRole('button', { name: 'Confirm cancellation' }))

    expect(await within(dialog).findByText('The service does not run on any date in that range.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Reason')).toHaveValue('x')
  })

  it('says "Already cancelled" for a repeat that the server answers with 0 journeys', async () => {
    const user = userEvent.setup()
    const existing = cancellation({ from: '2026-10-23', to: '2026-10-23' })
    const post = posts({ '/cancel': [{ cancellationId: 'c1', cancelledJourneys: 0, from: '2026-10-23', to: '2026-10-23' }] })
    renderWithApi(pages, fakeApi({ detail: detail({ cancellations: [existing] }) }, post), '/timetable/s1')

    const dialog = await openDialog(user)
    fireEvent.change(within(dialog).getByLabelText('Date'), { target: { value: '2026-10-23' } })
    await user.type(within(dialog).getByLabelText('Reason'), 'x')
    await user.click(within(dialog).getByRole('button', { name: 'Confirm cancellation' }))

    expect(await within(dialog).findByText('Already cancelled. No journeys changed.')).toBeInTheDocument()
  })

  it('closes without sending when the service is kept', async () => {
    const user = userEvent.setup()
    const post = posts({ '/cancel': [{}] })
    renderWithApi(pages, fakeApi({}, post), '/timetable/s1')

    const dialog = await openDialog(user)
    await user.click(within(dialog).getByRole('button', { name: 'Keep the service' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('offers no delete anywhere on the service screen', async () => {
    renderWithApi(pages, fakeApi(), '/timetable/s1')

    await screen.findByRole('button', { name: 'Cancel service' })
    expect(screen.queryByRole('button', { name: /delete|remove/i })).toBeNull()
    expect(screen.queryByRole('link', { name: /delete|remove/i })).toBeNull()
    expect(screen.getByText(/never deleted/)).toBeInTheDocument()
  })
})

describe('Amend a service (spec 15 W4)', () => {
  it('offers Amend on the newest version of a weekly service, and a link to the current one on an older version', async () => {
    const newest = detail()
    renderWithApi(pages, fakeApi({ detail: newest }), '/timetable/s1')
    expect(await screen.findByRole('link', { name: 'Amend service' })).toHaveAttribute('href', '/timetable/s1/amend')
  })

  it('shows the way to the current version on an older version', async () => {
    renderWithApi(pages, fakeApi({ detail: detail({ serviceId: 's0' }) }), '/timetable/s0')

    expect(await screen.findByText('This is an older version.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to the current version' })).toHaveAttribute('href', '/timetable/s1')
    expect(screen.queryByRole('link', { name: 'Amend service' })).toBeNull()
  })

  it('explains that an extra train cannot be amended', async () => {
    renderWithApi(pages, fakeApi({ detail: detail({ kind: 'Extra', runsOn: null, versions: [] }) }), '/timetable/s1')

    expect(await screen.findByText('To change an extra train, cancel it and add another.')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Amend service' })).toBeNull()
  })

  it('is filled with the current version, keeps the train number, category and type fixed, and starts tomorrow at the earliest', async () => {
    renderWithApi(pages, fakeApi(), '/timetable/s1/amend')

    expect(await screen.findByRole('heading', { level: 1, name: 'Amend 8072' })).toBeInTheDocument()
    expect(screen.getByLabelText('Train number')).toHaveValue('8072')
    expect(screen.getByLabelText('Train number')).toHaveAttribute('readonly')
    expect(screen.getByLabelText('Category')).toBeDisabled()
    expect(screen.getByLabelText('Type')).toBeDisabled()
    expect(screen.getByLabelText('Type')).toHaveValue('Express')
    expect(screen.getByRole('checkbox', { name: 'Monday' })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Saturday' })).not.toBeChecked()
    expect(screen.getByLabelText('Effective from')).toHaveAttribute('min')
    expect(screen.getByText(/cannot change in an amendment/)).toBeInTheDocument()
  })

  it('carries the current stops and times to step 2, with the stops the train skips shown as such', async () => {
    const user = userEvent.setup()
    renderWithApi(pages, fakeApi(), '/timetable/s1/amend')

    await user.click(await screen.findByRole('button', { name: 'Next' }))

    expect(await screen.findByLabelText('Time Veyangoda')).toHaveValue('17:30')
    expect(screen.getByLabelText('Time Colombo Fort')).toHaveValue('17:58')
    expect(screen.getByLabelText('Platform Veyangoda')).toHaveValue('1')
    expect(screen.getByRole('radio', { name: /Choose stops/ })).toBeChecked()
    const skipped = screen.getByRole('checkbox', { name: 'Include Gampaha' })
    expect(skipped).not.toBeChecked()
    expect(within(skipped.closest('tr')!).getByText('Train does not stop here')).toBeInTheDocument()
  })

  it('sends the amendment to its own route, then shows the new version and the counts', async () => {
    const user = userEvent.setup()
    const post = posts({
      '/validate': [valid],
      '/amend': [{ serviceId: 's2', version: 3, journeysUpdated: 3, journeysCancelled: 1, journeysGenerated: 0, warnings: [] }],
    })
    renderWithApi(pages, fakeApi({}, post), '/timetable/s1/amend')

    fireEvent.change(await screen.findByLabelText('Effective from'), { target: { value: '2099-10-26' } })
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.clear(await screen.findByLabelText('Time Veyangoda'))
    await user.type(screen.getByLabelText('Time Veyangoda'), '17:42')
    await user.click(screen.getByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    await waitFor(() => expect(post.mock.calls.some((c) => c[0] === '/api/v1/timetable/services/s1/amend')).toBe(true))
    const send = post.mock.calls.find((c) => c[0] === '/api/v1/timetable/services/s1/amend')!
    expect(send[1]).toMatchObject({ kind: 'Weekly', trainNumber: '8072', effectiveFrom: '2099-10-26' })
    expect((send[1] as { stops: { departure: string }[] }).stops[0]!.departure).toBe('17:42')
  })

  it('words a refused amendment: a journey with reports, and a date not after the current version', async () => {
    const user = userEvent.setup()
    const blocked = new ApiError(409, 'journey_has_events', { reason: 'journey_has_events', date: '2099-10-26' })
    renderWithApi(pages, fakeApi({}, posts({ '/validate': [valid], '/amend': [blocked] })), '/timetable/s1/amend')

    fireEvent.change(await screen.findByLabelText('Effective from'), { target: { value: '2099-10-26' } })
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.click(await screen.findByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText(/already has reports, so this amendment cannot start then/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Schedule service' })).toBeEnabled() // the draft is kept
  })
})
