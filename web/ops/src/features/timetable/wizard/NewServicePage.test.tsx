import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../../core/api/client'
import type { ValidationResult } from '../../../core/api/types'
import { renderWithApi } from '../../../test/helpers'
import { fakeApi, valid } from '../fixtures'
import { NewServicePage } from './NewServicePage'

const pages = (kind: 'Weekly' | 'Extra' = 'Weekly') => (
  <Routes>
    <Route path="timetable/new" element={<NewServicePage kind={kind} />} />
    <Route path="timetable/extra/new" element={<NewServicePage kind="Extra" />} />
    <Route path="timetable/extra" element={<p>extra trains page</p>} />
    <Route path="timetable/:serviceId" element={<p>service detail page</p>} />
  </Routes>
)

const created = { serviceIds: ['new-1'], trainNumber: '8072', version: 1, journeysGenerated: 5, warnings: [] }

/** A post that answers validate with `check` and create with the next of `creates` (a value to return, or an error to throw). */
function posts(check: ValidationResult | (() => ValidationResult), creates: unknown[] = [created]) {
  const queue = [...creates]
  return vi.fn(async (path: string) => {
    if (path.endsWith('/validate')) return typeof check === 'function' ? check() : check
    const next = queue.length > 1 ? queue.shift() : queue[0]
    if (next instanceof Error) throw next
    return next
  }) as unknown as Api['post'] & ReturnType<typeof vi.fn>
}

async function fillDetails(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByLabelText('Train number'), '8072')
  await user.click(screen.getByRole('checkbox', { name: 'Monday' }))
  await user.click(screen.getByRole('checkbox', { name: 'Friday' }))
  await user.click(screen.getByRole('button', { name: 'Next' }))
}

async function fillTimes(user: ReturnType<typeof userEvent.setup>) {
  const times: Record<string, string> = { Veyangoda: '17:25', Gampaha: '17:35', Ragama: '17:45', Maradana: '17:52', 'Colombo Fort': '17:58' }
  for (const [name, time] of Object.entries(times)) {
    await user.type(screen.getByLabelText(`Time ${name}`), time)
  }
}

describe('New weekly service (spec 15, W2)', () => {
  it('goes through the three steps, asks the server on Review and sends the request, then opens the service', async () => {
    const user = userEvent.setup()
    const post = posts(valid)
    renderWithApi(pages(), fakeApi({}, post), '/timetable/new')

    await fillDetails(user)
    expect(await screen.findByRole('heading', { level: 2, name: 'Stops and times' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /Every stop/ })).toBeChecked()
    await fillTimes(user)
    await user.selectOptions(screen.getByLabelText('Platform Gampaha'), '2')
    await user.click(screen.getByRole('button', { name: 'Next: review' }))

    expect(await screen.findByText('The server found no problems.')).toBeInTheDocument()
    const check = post.mock.calls[0]!
    expect(check[0]).toBe('/api/v1/timetable/validate')
    expect(check[1]).toMatchObject({ kind: 'Weekly', trainNumber: '8072', days: ['Mon', 'Fri'], lineCode: 'MAIN' })
    expect(screen.getByText(/17:25 · Veyangoda/)).toBeInTheDocument()
    expect(screen.getByText(/17:35 · Gampaha · Platform 2/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText('service detail page')).toBeInTheDocument()
    const send = post.mock.calls.find((c) => c[0] === '/api/v1/timetable/services')!
    expect((send[1] as { stops: unknown[] }).stops).toEqual([
      { stationCode: 'VYG', arrival: null, departure: '17:25', platform: null },
      { stationCode: 'GPH', arrival: '17:35', departure: '17:36', platform: '2' },
      { stationCode: 'RGM', arrival: '17:45', departure: '17:46', platform: null },
      { stationCode: 'MDA', arrival: '17:52', departure: '17:53', platform: null },
      { stationCode: 'CF', arrival: '17:58', departure: null, platform: null },
    ])
  })

  it('will not leave step 1 without a train number and a day, and says why', async () => {
    const user = userEvent.setup()
    renderWithApi(pages(), fakeApi(), '/timetable/new')

    await user.click(await screen.findByRole('button', { name: 'Next' }))

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText(/Enter the train number/)).toBeInTheDocument()
    expect(within(alert).getByText(/Choose at least one day/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'Details' })).toBeInTheDocument()
  })

  it('will not leave step 2 without a time for every included stop', async () => {
    const user = userEvent.setup()
    renderWithApi(pages(), fakeApi(), '/timetable/new')
    await fillDetails(user)

    await user.click(await screen.findByRole('button', { name: 'Next: review' }))

    expect(await screen.findByText(/Enter a time for every stop/)).toBeInTheDocument()
  })

  it('Choose stops shows a checkbox per station, locks the first and last, and shows "Train does not stop here"', async () => {
    const user = userEvent.setup()
    renderWithApi(pages(), fakeApi(), '/timetable/new')
    await fillDetails(user)

    await user.click(await screen.findByRole('radio', { name: /Choose stops/ }))

    expect(screen.getByRole('checkbox', { name: 'Include Veyangoda' })).toBeDisabled()
    expect(screen.getByRole('checkbox', { name: 'Include Colombo Fort' })).toBeDisabled()
    const ragama = screen.getByRole('checkbox', { name: 'Include Ragama' })
    expect(ragama).toBeEnabled()
    await user.click(ragama)
    const row = ragama.closest('tr')!
    expect(within(row).getByText('Train does not stop here')).toBeInTheDocument()
    expect(within(row).queryByLabelText('Time Ragama')).toBeNull()
    // Every stop brings it back.
    await user.click(screen.getByRole('radio', { name: /Every stop/ }))
    expect(screen.getByLabelText('Time Ragama')).toBeInTheDocument()
  })

  it('offers the platforms of each station and "Not set", never free text', async () => {
    const user = userEvent.setup()
    renderWithApi(pages(), fakeApi(), '/timetable/new')
    await fillDetails(user)

    const colomboFort = await screen.findByLabelText('Platform Colombo Fort')
    const veyangoda = screen.getByLabelText('Platform Veyangoda')

    expect(within(colomboFort).getAllByRole('option').map((o) => o.textContent)).toEqual(['Not set', 'Platform 1', 'Platform 2', 'Platform 3'])
    expect(within(veyangoda).getAllByRole('option').map((o) => o.textContent)).toEqual(['Not set', 'Platform 1', 'Platform 2'])
  })

  it('shows the server errors on Review and next to the stop they belong to, and keeps Schedule off', async () => {
    const user = userEvent.setup()
    const errors: ValidationResult = {
      valid: false,
      errors: [{ code: 'times_not_increasing', field: 'stops[2].arrival', message: 'Ragama 17:30 is not after 17:36.' }],
      warnings: [],
    }
    const post = posts(errors)
    renderWithApi(pages(), fakeApi({}, post), '/timetable/new')
    await fillDetails(user)
    await fillTimes(user)

    await user.click(await screen.findByRole('button', { name: 'Next: review' }))

    const summary = await screen.findByRole('alert')
    expect(within(summary).getByText('1 error. Fix it to schedule.')).toBeInTheDocument()
    expect(within(summary).getByText(/Ragama 17:30 is not after 17:36/)).toBeInTheDocument()
    expect(within(summary).getByText(/Error:/)).toBeInTheDocument() // the word, not only a colour
    expect(screen.getByRole('button', { name: 'Schedule service' })).toBeDisabled()
    await waitFor(() => expect(summary.parentElement).toHaveFocus()) // the wrapper of the summary takes focus

    // Back to the stops: the same error is beside Ragama.
    await user.click(screen.getByRole('button', { name: 'Back' }))
    const row = (await screen.findByLabelText('Time Ragama')).closest('tr')!
    expect(within(row).getByText(/Ragama 17:30 is not after 17:36/)).toBeInTheDocument()
    expect(within(row).getByText(/Error:/)).toBeInTheDocument()
  })

  it('shows advice, needs the box ticked, never blocks, and then sends acknowledgeWarnings', async () => {
    const user = userEvent.setup()
    const advice: ValidationResult = {
      valid: true,
      errors: [],
      warnings: [{ code: 'platform_close', field: 'stops[1].platform', message: 'Train 8065 uses GPH platform 2 at 17:31 (4 minutes apart).' }],
    }
    const post = posts(advice)
    renderWithApi(pages(), fakeApi({}, post), '/timetable/new')
    await fillDetails(user)
    await fillTimes(user)
    await user.click(await screen.findByRole('button', { name: 'Next: review' }))

    expect(await screen.findByText(/Train 8065 uses GPH platform 2/)).toBeInTheDocument()
    expect(screen.getByText(/Advice:/)).toBeInTheDocument()
    const schedule = screen.getByRole('button', { name: 'Schedule service' })
    expect(schedule).toBeDisabled()

    await user.click(screen.getByRole('checkbox', { name: /I have read the advice/ }))
    expect(schedule).toBeEnabled()
    await user.click(schedule)

    await screen.findByText('service detail page')
    const send = post.mock.calls.find((c) => c[0] === '/api/v1/timetable/services')!
    expect(send[1]).toMatchObject({ acknowledgeWarnings: true })
  })

  it('shows the errors the server finds when it refuses Schedule itself (422), and creates nothing', async () => {
    const user = userEvent.setup()
    const refused = new ApiError(422, 'invalid_service', {
      reason: 'invalid_service',
      errors: [{ code: 'platform_not_at_station', field: 'stops[0].platform', message: 'Platform 9 is not a platform of Veyangoda.' }],
      warnings: [],
    })
    renderWithApi(pages(), fakeApi({}, posts(valid, [refused])), '/timetable/new')
    await fillDetails(user)
    await fillTimes(user)
    await user.click(await screen.findByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText(/Platform 9 is not a platform of Veyangoda/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Schedule service' })).toBeDisabled()
    expect(screen.queryByText('service detail page')).toBeNull()
  })

  it('answers a 409 train number in use in words and keeps the draft', async () => {
    const user = userEvent.setup()
    const clash = new ApiError(409, 'train_number_in_use', { reason: 'train_number_in_use', trainNumber: '8072' })
    renderWithApi(pages(), fakeApi({}, posts(valid, [clash])), '/timetable/new')
    await fillDetails(user)
    await fillTimes(user)
    await user.click(await screen.findByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText('This train number already runs on these dates.')).toBeInTheDocument()
    expect(screen.getByText(/17:25 · Veyangoda/)).toBeInTheDocument() // the draft is still there
    expect(screen.getByRole('button', { name: 'Schedule service' })).toBeEnabled()
  })

  it('says nothing was saved when the call fails, and a retry sends one more request', async () => {
    const user = userEvent.setup()
    const post = posts(valid, [new ApiError(500, null), created])
    renderWithApi(pages(), fakeApi({}, post), '/timetable/new')
    await fillDetails(user)
    await fillTimes(user)
    await user.click(await screen.findByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText('Could not schedule. Nothing was saved. Try again.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText('service detail page')).toBeInTheDocument()
  })

  it('asks again when the server says the advice changed (409 warnings_not_acknowledged)', async () => {
    const user = userEvent.setup()
    const changed = new ApiError(409, 'warnings_not_acknowledged', { reason: 'warnings_not_acknowledged' })
    const post = posts(valid, [changed])
    renderWithApi(pages(), fakeApi({}, post), '/timetable/new')
    await fillDetails(user)
    await fillTimes(user)
    await user.click(await screen.findByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText('The advice changed. Read it again and confirm.')).toBeInTheDocument()
    expect(post.mock.calls.filter((c) => c[0] === '/api/v1/timetable/validate')).toHaveLength(2)
  })

  it('shows a Review check that failed with Try again', async () => {
    const user = userEvent.setup()
    const post = vi.fn().mockRejectedValueOnce(new ApiError(500, null)).mockResolvedValue(valid)
    renderWithApi(pages(), fakeApi({}, post as unknown as Api['post']), '/timetable/new')
    await fillDetails(user)
    await fillTimes(user)
    await user.click(await screen.findByRole('button', { name: 'Next: review' }))

    expect(await screen.findByText('Could not check the service. Nothing was saved. Try again.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('The server found no problems.')).toBeInTheDocument()
  })

  it('moves focus to the step heading when the step changes, and marks the current step', async () => {
    const user = userEvent.setup()
    renderWithApi(pages(), fakeApi(), '/timetable/new')
    await fillDetails(user)

    const heading = await screen.findByRole('heading', { level: 2, name: 'Stops and times' })
    await waitFor(() => expect(heading).toHaveFocus())
    expect(screen.getByRole('listitem', { current: 'step' })).toHaveTextContent('Stops and times')
  })

  it('has a visible label for every field of step 1', async () => {
    renderWithApi(pages(), fakeApi(), '/timetable/new')

    for (const label of ['Train number', 'Category', 'Type', 'First date it runs']) {
      expect(await screen.findByLabelText(label)).toBeInTheDocument()
    }
    expect(screen.getByRole('group', { name: 'Runs on' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Direction' })).toBeInTheDocument()
  })
})

describe('New extra train (spec 15, W2)', () => {
  it('adds the date shown in the box when Next is pressed without Add date, so a picked date is not lost', async () => {
    const user = userEvent.setup()
    const post = posts(valid, [{ ...created, serviceIds: ['a'] }])
    renderWithApi(pages('Extra'), fakeApi({}, post), '/timetable/extra/new')

    await user.type(await screen.findByLabelText('Train number'), '8099')
    fireEvent.change(screen.getByLabelText('Dates'), { target: { value: '2099-10-22' } })
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await fillTimes(user)
    await user.click(screen.getByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    await screen.findByText('extra trains page')
    const send = post.mock.calls.find((c) => c[0] === '/api/v1/timetable/services')!
    expect(send[1]).toMatchObject({ kind: 'Extra', dates: ['2099-10-22'] })
  })

  it('still asks for a date when none was picked or added', async () => {
    const user = userEvent.setup()
    renderWithApi(pages('Extra'), fakeApi(), '/timetable/extra/new')

    await user.type(await screen.findByLabelText('Train number'), '8099')
    fireEvent.change(screen.getByLabelText('Dates'), { target: { value: '' } })
    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText(/Choose at least one date/)).toBeInTheDocument()
  })

  it('takes dates instead of days, creates one request with all the dates and lands on the Extra trains tab with a message', async () => {
    const user = userEvent.setup()
    const post = posts(valid, [{ ...created, serviceIds: ['a', 'b'] }])
    renderWithApi(pages('Extra'), fakeApi({}, post), '/timetable/extra/new')

    await user.type(await screen.findByLabelText('Train number'), '8099')
    expect(screen.queryByRole('checkbox', { name: 'Monday' })).toBeNull()
    expect(screen.getByText('No date chosen yet.')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Dates'), { target: { value: '2026-10-22' } })
    await user.click(screen.getByRole('button', { name: 'Add date' }))
    fireEvent.change(screen.getByLabelText('Dates'), { target: { value: '2026-10-21' } })
    await user.click(screen.getByRole('button', { name: 'Add date' }))
    expect(screen.getByRole('button', { name: 'Remove Thu 22 Oct' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await fillTimes(user)
    await user.click(screen.getByRole('button', { name: 'Next: review' }))
    await user.click(await screen.findByRole('button', { name: 'Schedule service' }))

    expect(await screen.findByText('extra trains page')).toBeInTheDocument()
    const send = post.mock.calls.find((c) => c[0] === '/api/v1/timetable/services')!
    expect(send[1]).toMatchObject({ kind: 'Extra', trainNumber: '8099', dates: ['2026-10-21', '2026-10-22'] })
    expect((send[1] as Record<string, unknown>).days).toBeUndefined()
  })
})
