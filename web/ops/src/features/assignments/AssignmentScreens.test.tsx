import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../core/api/client'
import type { AssignmentJourney, AssignmentView, OperatorChoice } from '../../core/api/types'
import { renderWithApi } from '../../test/helpers'
import { AssignPage } from './AssignPage'
import { JourneyCrewPage } from './JourneyCrewPage'
import { JourneysPage } from './JourneysPage'

const journey = (overrides: Partial<AssignmentJourney> = {}): AssignmentJourney => ({
  journeyId: 'j1',
  trainNumber: '8072',
  from: 'Veyangoda',
  to: 'Colombo Fort',
  operatingDate: '2026-10-23',
  firstDeparture: '2026-10-23T11:55:00+00:00',
  lastArrival: '2026-10-23T12:28:00+00:00',
  status: 'Unassigned',
  source: 'Weekly',
  operator: null,
  ...overrides,
})

const choice = (overrides: Partial<OperatorChoice> = {}): OperatorChoice => ({
  userId: 'op-1',
  name: 'Nimal Perera',
  enabled: true,
  available: true,
  busyOn: null,
  ...overrides,
})

const assignment = (overrides: Partial<AssignmentView> = {}): AssignmentView => ({
  assignmentId: 'a1',
  operatorUserId: 'op-1',
  operatorName: 'Nimal Perera',
  validFrom: '2026-10-23T11:25:00+00:00',
  validTo: null,
  assignedBy: 'jayasinghe-sub',
  endedBy: null,
  endReason: null,
  endNote: null,
  ...overrides,
})

interface Reads {
  unassigned?: AssignmentJourney[]
  all?: AssignmentJourney[]
  operators?: OperatorChoice[]
  assignments?: AssignmentView[]
}

function fakeApi(reads: Reads = {}, post: Api['post'] = vi.fn(async () => ({}))) {
  const get = vi.fn(async (path: string) => {
    if (path.includes('/assignments/journeys') && path.includes('filter=unassigned')) return reads.unassigned ?? [journey()]
    if (path.includes('/assignments/journeys')) return reads.all ?? [journey()]
    if (path.includes('/assignments/operators')) return reads.operators ?? [choice(), choice({ userId: 'op-2', name: 'Second Operator' })]
    if (path.includes('/assignments')) return reads.assignments ?? []
    throw new Error(`unexpected GET ${path}`)
  })
  return { get: get as unknown as Api['get'], post } satisfies Api
}

const screens = (
  <Routes>
    <Route path="journeys" element={<JourneysPage />} />
    <Route path="journeys/all" element={<JourneysPage all />} />
    <Route path="journeys/:journeyId" element={<JourneyCrewPage />} />
    <Route path="journeys/:journeyId/assign" element={<AssignPage />} />
  </Routes>
)

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-23T11:30:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
})

describe('Journeys: the Unassigned list (spec 17 A2)', () => {
  it('lists the journeys nobody covers, with "No operator assigned" in words and an Assign operator link', async () => {
    renderWithApi(screens, fakeApi(), '/journeys?date=2026-10-23')

    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(2)
    const row = within(rows[1]!)
    expect(row.getByText('8072')).toBeInTheDocument()
    expect(row.getByText('Veyangoda to Colombo Fort')).toBeInTheDocument()
    expect(row.getByText(/17:25/)).toBeInTheDocument() // Sri Lanka time
    expect(row.getByText('Weekly schedule')).toBeInTheDocument()
    expect(row.getByText('No operator assigned')).toBeInTheDocument()
    expect(row.getByRole('link', { name: 'Assign operator' })).toHaveAttribute('href', '/journeys/j1/assign?date=2026-10-23')
  })

  it('shows the tab counts and an empty state', async () => {
    renderWithApi(screens, fakeApi({ unassigned: [], all: [journey({ operator: { userId: 'op-1', name: 'Nimal Perera', validFrom: 'x', validTo: null } })] }), '/journeys?date=2026-10-23')

    expect(await screen.findByLabelText('0 Unassigned')).toBeInTheDocument()
    expect(await screen.findByLabelText('1 All journeys')).toBeInTheDocument()
    expect(screen.getByText('Every journey of this day has an operator.')).toBeInTheDocument()
  })

  it('All journeys shows the operator by name and links to Crew and control', async () => {
    const api = fakeApi({ all: [journey({ operator: { userId: 'op-1', name: 'Nimal Perera', validFrom: 'x', validTo: null } })] })
    renderWithApi(screens, api, '/journeys/all?date=2026-10-23')

    const row = within((await screen.findAllByRole('row'))[1]!)
    expect(row.getByText('Nimal Perera')).toBeInTheDocument()
    expect(row.getByRole('link', { name: 'Crew and control' })).toHaveAttribute('href', '/journeys/j1/crew?date=2026-10-23')
  })

  it('filters the journeys by where they are in the day and by train number, with counts, and clears the filters', async () => {
    const user = userEvent.setup()
    const all = [
      journey({ journeyId: 'a', trainNumber: '8104', status: 'Running' }),
      journey({ journeyId: 'b', trainNumber: '8106', status: 'Scheduled' }),
      journey({ journeyId: 'c', trainNumber: '8108', status: 'Completed' }),
    ]
    renderWithApi(screens, fakeApi({ unassigned: [], all }), '/journeys/all?date=2026-10-23')

    expect(await screen.findByText('Showing 3 of 3 journeys')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Ongoing\s*\(1\)/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Not started yet\s*\(1\)/ })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Ongoing/ }))
    expect(screen.getByText('Showing 1 of 3 journeys')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '8104' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: '8106' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Ongoing/ })).toHaveAttribute('aria-pressed', 'true')

    await user.click(screen.getByRole('button', { name: /^All/ }))
    await user.type(screen.getByLabelText('Train number'), '8108')
    expect(screen.getByText('Showing 1 of 3 journeys')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '8108' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Ongoing/ }))
    expect(screen.getByText('No journey matches these filters.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    expect(await screen.findByText('Showing 3 of 3 journeys')).toBeInTheDocument()
  })

  it('has an Overdue to start filter for journeys that nothing has started, apart from Not started yet', async () => {
    const user = userEvent.setup()
    const all = [
      journey({ journeyId: 'a', trainNumber: '8061', status: 'Scheduled', overdueToStart: true }),
      journey({ journeyId: 'b', trainNumber: '8106', status: 'Scheduled' }),
    ]
    renderWithApi(screens, fakeApi({ unassigned: [], all }), '/journeys/all?date=2026-10-23')

    expect(await screen.findByRole('button', { name: /^Overdue to start\s*\(1\)/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Not started yet\s*\(1\)/ })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /^Overdue to start/ }))

    expect(screen.getByRole('link', { name: '8061' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: '8106' })).not.toBeInTheDocument()
  })

  it('shows the status of each journey as words with an icon', async () => {
    renderWithApi(screens, fakeApi({ unassigned: [], all: [journey({ status: 'Running' })] }), '/journeys/all?date=2026-10-23')

    expect(await screen.findByText('Ongoing', { selector: 'span.inline-flex' })).toBeInTheDocument()
  })

  it('says it could not load and offers Try again', async () => {
    const user = userEvent.setup()
    const get = vi.fn().mockRejectedValueOnce(new ApiError(500, null)).mockRejectedValueOnce(new ApiError(500, null)).mockResolvedValue([journey()])
    renderWithApi(screens, { get: get as unknown as Api['get'], post: vi.fn() }, '/journeys?date=2026-10-23')

    expect(await screen.findByText('Could not load the journeys. Try again.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('8072')).toBeInTheDocument()
  })
})

describe('Assign an operator (spec 17 A2)', () => {
  it('lists the operators with who is available and who is busy on which train, and sends the choice with the defaults', async () => {
    const user = userEvent.setup()
    const post = vi.fn(async () => ({ assignmentId: 'n', operatorUserId: 'op-2', name: 'Second Operator', validFrom: 'x', validTo: null, journeyStatus: 'Scheduled', ended: null }))
    const api = fakeApi({ operators: [choice({ available: false, busyOn: { journeyId: 'j9', trainNumber: '8058', until: '2026-10-23T13:21:00+00:00' } }), choice({ userId: 'op-2', name: 'Second Operator' })] }, post)
    renderWithApi(screens, api, '/journeys/j1/assign?date=2026-10-23')

    expect(await screen.findByRole('heading', { level: 1, name: 'Assign an operator to 8072' })).toBeInTheDocument()
    expect(await screen.findByText('On train 8058 until Fri 23 Oct · 18:51')).toBeInTheDocument() // the day is shown, so 00:00 is not mistaken for "now"
    expect(screen.getByRole('radio', { name: /Nimal Perera/ })).toBeDisabled() // busy: not chosen
    expect(screen.getByText(/cannot be chosen/)).toBeInTheDocument()
    expect(screen.getByText('Available')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: /Second Operator/ }))
    await user.click(screen.getByRole('button', { name: 'Assign operator' }))

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(post).toHaveBeenCalledWith('/api/v1/journeys/j1/assignments', { operatorUserId: 'op-2' })
  })

  it('needs an operator and a valid time before it sends anything', async () => {
    const user = userEvent.setup()
    const post = vi.fn(async () => ({}))
    renderWithApi(screens, fakeApi({}, post), '/journeys/j1/assign?date=2026-10-23')

    await user.click(await screen.findByRole('button', { name: 'Assign operator' }))
    expect(await screen.findByText('Choose an operator.')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: /Nimal Perera/ }))
    await user.click(screen.getByRole('radio', { name: 'From a time' }))
    await user.type(screen.getByLabelText('From: Time (HH:mm)'), '9:30')
    await user.click(screen.getByRole('button', { name: 'Assign operator' }))

    expect(await screen.findByText('Enter the time as HH:mm.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('sends a start and an end on the journey day in Sri Lanka time', async () => {
    const user = userEvent.setup()
    const post = vi.fn(async () => ({ name: 'Nimal Perera' }))
    renderWithApi(screens, fakeApi({}, post), '/journeys/j1/assign?date=2026-10-23')

    await user.click(await screen.findByRole('radio', { name: /Nimal Perera/ }))
    await user.click(screen.getByRole('radio', { name: 'From a time' }))
    await user.type(screen.getByLabelText('From: Time (HH:mm)'), '17:00')
    await user.click(screen.getByRole('radio', { name: 'Until a time' }))
    await user.type(screen.getByLabelText('To: Time (HH:mm)'), '18:30')
    await user.click(screen.getByRole('button', { name: 'Assign operator' }))

    await waitFor(() => expect(post).toHaveBeenCalled())
    expect(post).toHaveBeenCalledWith('/api/v1/journeys/j1/assignments', {
      operatorUserId: 'op-1',
      validFrom: '2026-10-23T17:00:00+05:30',
      validTo: '2026-10-23T18:30:00+05:30',
    })
  })

  it('refuses an operator on another train even if the list was out of date, names who and until when, and sends no acknowledgement', async () => {
    const user = userEvent.setup()
    const busy = new ApiError(409, 'operator_overlaps_another_journey', {
      reason: 'operator_overlaps_another_journey',
      journeyId: 'j9',
      trainNumber: '8058',
      until: '2026-10-23T13:21:00+00:00',
    })
    const post = vi.fn().mockRejectedValue(busy)
    renderWithApi(screens, fakeApi({}, post as unknown as Api['post']), '/journeys/j1/assign?date=2026-10-23')

    await user.click(await screen.findByRole('radio', { name: /Nimal Perera/ })) // the list said available
    await user.click(screen.getByRole('button', { name: 'Assign operator' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Nimal Perera is on train 8058 until Fri 23 Oct · 18:51, so cannot be assigned here. Nothing was changed.')
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument() // no "assign anyway" box any more
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/api/v1/journeys/j1/assignments', { operatorUserId: 'op-1' })
  })

  it('words a refused assignment in plain language', async () => {
    const user = userEvent.setup()
    const post = vi.fn().mockRejectedValue(new ApiError(409, 'overlaps_later_assignment', { reason: 'overlaps_later_assignment' }))
    renderWithApi(screens, fakeApi({}, post as unknown as Api['post']), '/journeys/j1/assign?date=2026-10-23')

    await user.click(await screen.findByRole('radio', { name: /Nimal Perera/ }))
    await user.click(screen.getByRole('button', { name: 'Assign operator' }))

    expect(await screen.findByText(/Another assignment starts at or after this start/)).toBeInTheDocument()
  })

  it('says the operators could not be loaded and keeps Assign useless until they are', async () => {
    const get = vi.fn(async (path: string) => {
      if (path.includes('/assignments/operators')) throw new ApiError(503, 'operator_directory_unavailable')
      return [journey()]
    })
    renderWithApi(screens, { get: get as unknown as Api['get'], post: vi.fn() }, '/journeys/j1/assign?date=2026-10-23')

    expect(await screen.findByText('Operators could not be loaded. Try again.')).toBeInTheDocument()
    expect(screen.queryByRole('radio', { name: /Nimal/ })).toBeNull()
  })

  it('shows a disabled account but does not let it be chosen', async () => {
    renderWithApi(screens, fakeApi({ operators: [choice({ enabled: false })] }), '/journeys/j1/assign?date=2026-10-23')

    expect(await screen.findByText('Account disabled')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /Nimal Perera/ })).toBeDisabled()
  })
})

describe('Crew and control, and End assignment (spec 17 A2)', () => {
  it('lists the assignments with who assigned them and how each ended, and offers End for the one running now', async () => {
    const api = fakeApi({
      assignments: [
        assignment({ assignmentId: 'old', operatorName: 'Second Operator', validFrom: '2026-10-23T09:00:00+00:00', validTo: '2026-10-23T11:00:00+00:00', endedBy: 'jayasinghe-sub', endReason: 'CrewChange' }),
        assignment({ assignmentId: 'now', validFrom: '2026-10-23T11:00:00+00:00' }),
      ],
    })
    renderWithApi(screens, api, '/journeys/j1?date=2026-10-23')

    const rows = await screen.findAllByRole('row')
    expect(within(rows[1]!).getByText('Crew change · by jayasinghe-sub')).toBeInTheDocument()
    expect(within(rows[2]!).getByText('Now')).toBeInTheDocument() // marked in words
    expect(within(rows[2]!).getByText('Until the journey ends')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'End assignment · Nimal Perera' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /End assignment · Second/ })).toBeNull()
    expect(screen.getByRole('link', { name: 'Assign operator' })).toHaveAttribute('href', '/journeys/j1/assign?date=2026-10-23')
  })

  it('says nobody has been assigned when there is no assignment', async () => {
    renderWithApi(screens, fakeApi({ assignments: [] }), '/journeys/j1?date=2026-10-23')

    expect(await screen.findByText('Nobody has been assigned to this journey.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /End assignment/ })).toBeNull()
  })

  it('asks first, says what a lost device means, and sends the reason and note', async () => {
    const user = userEvent.setup()
    const post = vi.fn(async () => ({}))
    renderWithApi(screens, fakeApi({ assignments: [assignment()] }, post), '/journeys/j1?date=2026-10-23')

    await user.click(await screen.findByRole('button', { name: 'End assignment · Nimal Perera' }))
    const dialog = screen.getByRole('dialog')
    expect(post).not.toHaveBeenCalled() // the dialog asks first
    expect(within(dialog).getByText(/including reports that claim an earlier time/)).toBeInTheDocument() // lost device is the first choice
    expect(within(dialog).getByRole('radio', { name: 'Lost or stolen device' })).toBeChecked()
    expect(within(dialog).getByText(/Nobody can report on this journey until you assign another operator/)).toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Note (optional)'), 'Phone lost at the platform')
    expect(within(dialog).getByText('26 / 500')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'End assignment' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(post).toHaveBeenCalledWith('/api/v1/journeys/j1/assignments/a1/end', { reason: 'LostDevice', note: 'Phone lost at the platform' })
    expect(await screen.findByText(/Assignment ended\./)).toBeInTheDocument()
  })

  it('says a different thing for another reason: a report made earlier and synced later is still accepted', async () => {
    const user = userEvent.setup()
    renderWithApi(screens, fakeApi({ assignments: [assignment()] }), '/journeys/j1?date=2026-10-23')

    await user.click(await screen.findByRole('button', { name: 'End assignment · Nimal Perera' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('radio', { name: 'Crew change' }))

    expect(within(dialog).getByText(/still accepted/)).toBeInTheDocument()
    expect(within(dialog).queryByText(/claim an earlier time/)).toBeNull()
  })

  it('sends one request on a double click, closes without sending when kept, and words an assignment that already ended', async () => {
    const user = userEvent.setup()
    let release: (value: unknown) => void = () => {}
    const post = vi.fn(() => new Promise((resolve) => (release = resolve))) as unknown as Api['post'] & ReturnType<typeof vi.fn>
    renderWithApi(screens, fakeApi({ assignments: [assignment()] }, post), '/journeys/j1?date=2026-10-23')

    await user.click(await screen.findByRole('button', { name: 'End assignment · Nimal Perera' }))
    const dialog = screen.getByRole('dialog')
    await user.dblClick(within(dialog).getByRole('button', { name: 'End assignment' }))
    expect(post).toHaveBeenCalledTimes(1)
    expect(within(dialog).getByRole('button', { name: 'Ending…' })).toBeDisabled()
    release({})
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('shows "already ended" in words and keeps the dialog open', async () => {
    const user = userEvent.setup()
    const post = vi.fn().mockRejectedValue(new ApiError(409, 'already_ended', { reason: 'already_ended' }))
    renderWithApi(screens, fakeApi({ assignments: [assignment()] }, post as unknown as Api['post']), '/journeys/j1?date=2026-10-23')

    await user.click(await screen.findByRole('button', { name: 'End assignment · Nimal Perera' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'End assignment' }))

    expect(await screen.findByText('This assignment has already ended.')).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('closes without sending when the assignment is kept', async () => {
    const user = userEvent.setup()
    const post = vi.fn(async () => ({}))
    renderWithApi(screens, fakeApi({ assignments: [assignment()] }, post), '/journeys/j1?date=2026-10-23')

    await user.click(await screen.findByRole('button', { name: 'End assignment · Nimal Perera' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Keep the assignment' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('has nothing that deletes an assignment', async () => {
    renderWithApi(screens, fakeApi({ assignments: [assignment()] }), '/journeys/j1?date=2026-10-23')

    await screen.findByRole('button', { name: 'End assignment · Nimal Perera' })
    expect(screen.queryByRole('button', { name: /delete|remove/i })).toBeNull()
    fireEvent.keyDown(document.body, { key: 'Escape' })
  })
})
