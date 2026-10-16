import { act, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { board, call, json, renderDisplay } from './fixtures'

// If a display ever asked for a sign-in, this would be called. It must not be.
const useAuth = vi.fn(() => ({ isLoading: false, isAuthenticated: false, signinRedirect: vi.fn() }))
vi.mock('react-oidc-context', () => ({ useAuth: () => useAuth() }))

beforeEach(() => {
  // Only the clock and intervals are faked: polling and the freshness tick use them; promises and the DOM stay real.
  vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
  vi.setSystemTime(new Date('2026-10-15T02:58:00Z'))
  useAuth.mockClear()
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

const pass = (ms: number) => act(async () => { await vi.advanceTimersByTimeAsync(ms) })

describe('Platform display', () => {
  it('shows the expected arrival as the largest element, with train, destination in three languages, status and no verification badge (AC-E09-02 1, 4)', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board()))

    const time = await screen.findByText('08:38') // 03:08 UTC in Sri Lanka
    expect(time).toHaveClass('text-level1-time')
    // It is the only element at that size on the screen: nothing else competes with it (D-50).
    expect(document.querySelectorAll('.text-level1-time')).toHaveLength(1)
    expect(screen.getByText('Platform 1')).toBeInTheDocument()
    expect(screen.getByText('Train 8058')).toBeInTheDocument()
    expect(screen.getByText('Colombo Fort')).toBeInTheDocument()
    expect(screen.getByText('කොළඹ කොටුව')).toBeInTheDocument()
    expect(screen.getByText('கொழும்பு கோட்டை')).toBeInTheDocument()
    expect(screen.getByText('Delayed 8 min')).toBeInTheDocument() // status as words (and an icon, not colour alone)
    expect(screen.getByText('Expected arrival')).toBeInTheDocument()
    expect(screen.getByText('අපේක්ෂිත පැමිණීම')).toBeInTheDocument()
    expect(screen.getByText('எதிர்பார்க்கப்படும் வருகை')).toBeInTheDocument()
    expect(screen.getByText('About 8 min late')).toBeInTheDocument()
    expect(screen.queryByText(/verified/i)).not.toBeInTheDocument()
  })

  it('says a long delay in hours, not minutes (4 h, not 240 min)', async () => {
    renderDisplay('/display/platform/GPH/1', () =>
      json(board({ trains: [call({ delayMinutes: 240, arrival: { scheduled: '2026-10-15T03:00:00Z', estimated: '2026-10-15T07:00:00Z' } })] })))

    expect(await screen.findByText('Delayed 4 h')).toBeInTheDocument()
    expect(screen.getByText('About 4 h late')).toBeInTheDocument()
    expect(screen.getByText('පැය 4ක් පමණ ප්‍රමාදයි')).toBeInTheDocument()
    expect(screen.getByText('சுமார் 4 மணி தாமதம்')).toBeInTheDocument()
    expect(screen.queryByText(/240/)).not.toBeInTheDocument()
  })

  it('marks every script with its language so a screen reader uses the right voice', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board()))

    await screen.findByText('08:38')
    expect(screen.getByText('කොළඹ කොටුව')).toHaveAttribute('lang', 'si')
    expect(screen.getByText('கொழும்பு கோட்டை')).toHaveAttribute('lang', 'ta')
    expect(screen.getByText('Colombo Fort')).toHaveAttribute('lang', 'en')
  })

  it('asks the API for this station and platform, with no token and no sign-in (spec 09 rule 14)', async () => {
    const { fetchMock } = renderDisplay('/display/platform/GPH/1', () => json(board()))

    await screen.findByText('08:38')
    expect(fetchMock).toHaveBeenCalledWith('http://localhost:8080/api/v1/stations/GPH/board?platform=1&count=6', expect.objectContaining({}))
    const init = fetchMock.mock.calls[0]![1] as RequestInit | undefined
    expect(init?.headers).toBeUndefined() // no Authorization header
    expect(useAuth).not.toHaveBeenCalled()
  })

  it('shows the time shown on the screen from the server, not from the browser (rule 11)', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board({ serverTime: '2026-10-15T02:58:00Z' })))

    expect(await screen.findByText('Updated 08:28')).toBeInTheDocument()
  })

  it('shows the corner clock with seconds, ticking every second from the server time, not the browser clock (rule 11)', async () => {
    // The browser clock is wrong on purpose: an hour behind the server. The clock must still follow the server.
    vi.setSystemTime(new Date('2026-10-15T01:58:00Z'))
    renderDisplay('/display/platform/GPH/1', () => json(board({ serverTime: '2026-10-15T02:58:00Z' })))

    expect(await screen.findByText('08:28:00')).toBeInTheDocument()
    await pass(3000)

    expect(screen.getByText('08:28:03')).toBeInTheDocument()
  })

  it('shows the first stop as a departure, labelled so', async () => {
    renderDisplay('/display/platform/VYG/1', () =>
      json(board({ trains: [call({ arrival: null, departure: { scheduled: '2026-10-15T02:50:00Z', estimated: '2026-10-15T02:50:00Z' }, statusKey: 'OnTime', delayMinutes: 0 })] })))

    expect(await screen.findByText('08:20')).toHaveClass('text-level1-time')
    expect(screen.getByText('Expected departure')).toBeInTheDocument()
    expect(screen.getAllByText('On time')).toHaveLength(2) // the chip and the line in three languages
  })

  it('says "At platform" with the departure time once the train has arrived (FR-81)', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board({ trains: [call({ state: 'AtPlatform' })] })))

    expect(await screen.findByText('At platform', { selector: 'span.text-level1-status' })).toBeInTheDocument()
    expect(screen.getByText(/Departs 08:39/)).toBeInTheDocument()
    expect(document.querySelectorAll('.text-level1-time')).toHaveLength(0)
  })

  it('says "Waiting for update" with no time at all when the delay is unknown, and where the train was last seen (D-39)', async () => {
    renderDisplay('/display/platform/GPH/1', () =>
      json(board({
        trains: [call({ delayKnown: false, arrival: { scheduled: '2026-10-15T03:00:00Z', estimated: null }, lastSeen: { state: 'Departed', stationName: 'Veyangoda', at: '2026-10-15T02:58:00Z' } })],
      })))

    expect(await screen.findByText('Waiting for update')).toBeInTheDocument()
    expect(screen.getByText('No news yet')).toBeInTheDocument()
    expect(screen.getByText(/Last seen: Departed Veyangoda/)).toHaveTextContent('08:28')
    expect(document.querySelectorAll('.text-level1-time')).toHaveLength(0) // no ETA
    expect(screen.queryByText('08:30')).not.toBeInTheDocument()
  })

  it('says "No trains until" with the next service when the platform is empty (AC-E09-04 1)', async () => {
    renderDisplay('/display/platform/GPH/2', () =>
      json(board({
        trains: [],
        next: call({ live: false, trainNumber: '8062', arrival: { scheduled: '2026-10-15T03:55:00Z', estimated: null }, platform: '2' }),
      })))

    expect(await screen.findByText('No trains until 09:25')).toBeInTheDocument()
    expect(screen.getByText('09:25 දක්වා දුම්රිය නැත')).toBeInTheDocument()
    expect(screen.getByText('Train 8062')).toBeInTheDocument()
    expect(screen.getByText('Next service')).toBeInTheDocument()
  })

  it('says "No more services today" and the first service tomorrow (AC-E09-04 2)', async () => {
    renderDisplay('/display/platform/GPH/1', () =>
      json(board({
        trains: [],
        firstTomorrow: call({ live: false, trainNumber: '8061', arrival: { scheduled: '2026-10-16T02:30:00Z', estimated: null } }),
      })))

    expect(await screen.findByText('No more services today')).toBeInTheDocument()
    expect(screen.getByText('First service tomorrow 08:00')).toBeInTheDocument()
  })

  it('says only "No more services today" when there is no service in the horizon', async () => {
    renderDisplay('/display/platform/GPH/2', () => json(board({ trains: [], next: null, firstTomorrow: null })))

    expect(await screen.findByText('No more services today')).toBeInTheDocument()
    expect(screen.queryByText(/First service/)).not.toBeInTheDocument()
  })

  it.each([
    ['an unknown station', '/display/platform/XXX/1', 404],
    ['an unknown platform', '/display/platform/GPH/9', 404],
  ])('says "This display is not set up" for %s', async (_name, path, status) => {
    renderDisplay(path, () => json({ reason: 'station_not_found' }, status))

    expect(await screen.findByText('This display is not set up')).toBeInTheDocument()
  })

  it('says "This display is not set up" for a URL that cannot be a station or platform, and asks the API nothing (rule 13)', async () => {
    const { fetchMock } = renderDisplay('/display/platform/<script>/1', () => json(board()))

    expect(await screen.findByText('This display is not set up')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

/** A call later in the day: `hour` and `minute` are Sri Lanka time. */
const later = (n: number, hour: number, minute: number, overrides = {}) =>
  call({
    trainNumber: `80${n}`, statusKey: 'Scheduled', statusGroup: 'Normal', delayMinutes: 0, live: false,
    destination: { code: 'D' + n, name: { en: `Town ${n}`, si: `නගරය ${n}`, ta: `நகரம் ${n}` } },
    arrival: { scheduled: `2026-10-15T${String(hour - 5).padStart(2, '0')}:${String(minute - 30 < 0 ? minute + 30 : minute - 30).padStart(2, '0')}:00Z`, estimated: null },
    ...overrides,
  })

describe('Platform display: next trains below the first (spec 09 section 16)', () => {
  it('lists the following trains under the first one, which stays the only largest element (D-50)', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board({ trains: [call(), later(1, 13, 30), later(2, 22, 20, { statusKey: 'Delayed', delayMinutes: 5 })] })))

    expect(await screen.findByText('08:38')).toHaveClass('text-level1-time')
    expect(document.querySelectorAll('.text-level1-time')).toHaveLength(1) // the upcoming rows are smaller
    const panel = within(screen.getByRole('region', { name: 'Next trains' }))
    const rows = panel.getAllByRole('listitem')
    expect(rows).toHaveLength(2)
    expect(within(rows[0]!).getByText('Town 1')).toBeInTheDocument()
    expect(within(rows[0]!).getByText('නගරය 1')).toBeInTheDocument()
    expect(within(rows[0]!).getByText('நகரம் 1')).toBeInTheDocument()
    expect(within(rows[0]!).getByText('Scheduled')).toBeInTheDocument() // status as words, with an icon
    expect(within(rows[1]!).getByText('Delayed 5 min')).toBeInTheDocument()
    expect(panel.getByText('ඊළඟ දුම්රිය')).toBeInTheDocument() // the Sinhala heading
    expect(panel.getByText('அடுத்த ரயில்கள்')).toBeInTheDocument()
  })

  it('shows no panel when there is only one train', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board()))

    await screen.findByText('08:38')
    expect(screen.queryByRole('region', { name: 'Next trains' })).not.toBeInTheDocument()
  })

  it('shows three at a time and rises to the next three every 8 seconds, then starts again', async () => {
    const many = [call(), ...[1, 2, 3, 4, 5].map((n) => later(n, 10 + n, 30))]
    renderDisplay('/display/platform/GPH/1', () => json(board({ trains: many })))
    await screen.findByText('08:38')
    const shown = () => screen.getAllByRole('listitem').map((row) => within(row).getByText(/^Town \d$/).textContent)

    expect(shown()).toEqual(['Town 1', 'Town 2', 'Town 3'])
    expect(screen.getByText('1 / 2')).toBeInTheDocument()

    await pass(8000)
    expect(shown()).toEqual(['Town 4', 'Town 5'])
    expect(screen.getByText('2 / 2')).toBeInTheDocument()

    await pass(8000)
    expect(shown()).toEqual(['Town 1', 'Town 2', 'Town 3'])
  })

  it('does not page when everything fits on one page', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board({ trains: [call(), later(1, 11, 30), later(2, 12, 30), later(3, 13, 30)] })))
    await screen.findByText('08:38')

    await pass(20_000)

    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    expect(screen.queryByText(/ \/ /)).not.toBeInTheDocument()
  })

  it('rises into place with an animation class that the stylesheet switches off for reduced motion', async () => {
    renderDisplay('/display/platform/GPH/1', () => json(board({ trains: [call(), later(1, 11, 30)] })))
    await screen.findByText('08:38')

    expect(screen.getByRole('region', { name: 'Next trains' }).querySelector('ul')).toHaveClass('animate-rise')
  })

  it('moves on when the first train leaves: the next train becomes Level 1 and the list rises one place', async () => {
    let current = board({ trains: [call(), later(1, 11, 30), later(2, 12, 30)] })
    renderDisplay('/display/platform/GPH/1', () => json(current))
    await screen.findByText('08:38')
    expect(screen.getAllByRole('listitem')).toHaveLength(2)

    current = board({ trains: [later(1, 11, 30), later(2, 12, 30)] })
    await pass(10_000)

    expect(await screen.findByText('11:30')).toHaveClass('text-level1-time')
    expect(screen.queryByText('Train 8058')).not.toBeInTheDocument()
    const rows = screen.getAllByRole('listitem')
    expect(rows).toHaveLength(1)
    expect(within(rows[0]!).getByText('Town 2')).toBeInTheDocument()
  })

  it('keeps the rows, greyed, when offline', async () => {
    let up = true
    renderDisplay('/display/platform/GPH/1', () => (up ? json(board({ trains: [call(), later(1, 11, 30)] })) : Promise.reject(new TypeError('network'))))
    await screen.findByText('08:38')
    up = false

    await pass(31_000)

    await screen.findByText('Live updates unavailable')
    const row = screen.getByRole('listitem')
    expect(within(row).getByText('Town 1')).toBeInTheDocument()
    expect(within(row).getByText('11:30')).toHaveClass('text-muted')
  })

  it('leaves the main board unchanged: it still lists every train in one table', async () => {
    renderDisplay('/display/board/GPH', () => json(board({ trains: [call(), later(1, 11, 30)] })))

    await screen.findByText('08:38')
    expect(screen.queryByRole('region', { name: 'Next trains' })).not.toBeInTheDocument()
    expect(screen.getAllByRole('listitem')).toHaveLength(2)
  })
})

describe('Platform display offline (D-39, NFR-03, AC-E09-03)', () => {
  it('shows "Live updates unavailable" with the last time after 30 s without an answer, keeps the train and marks the ETA as last known', async () => {
    let up = true
    renderDisplay('/display/platform/GPH/1', () => (up ? json(board()) : Promise.reject(new TypeError('network'))))
    expect(await screen.findByText('08:38')).not.toHaveClass('text-muted')
    expect(screen.queryByText('Live updates unavailable')).not.toBeInTheDocument()

    up = false
    await pass(31_000)

    const banner = await screen.findByRole('status')
    expect(within(banner).getByText('Live updates unavailable')).toBeInTheDocument()
    expect(within(banner).getByText('සජීවී යාවත්කාලීන නොමැත')).toBeInTheDocument()
    expect(within(banner).getByText('நேரடி புதுப்பிப்புகள் இல்லை')).toBeInTheDocument()
    expect(within(banner).getByText('Last update 08:28')).toBeInTheDocument() // the server's time of the last good answer
    expect(screen.getByText('Train 8058')).toBeInTheDocument() // identity stays
    expect(screen.getByText('08:38')).toHaveClass('text-muted') // marked
    expect(screen.getByText('Last known arrival')).toBeInTheDocument()
  })

  it('does not show the banner before 30 s', async () => {
    let up = true
    renderDisplay('/display/platform/GPH/1', () => (up ? json(board()) : Promise.reject(new TypeError('network'))))
    await screen.findByText('08:38')

    up = false
    await pass(20_000)

    expect(screen.queryByText('Live updates unavailable')).not.toBeInTheDocument()
  })

  it('returns to live without a reload when an answer comes back', async () => {
    let up = true
    renderDisplay('/display/platform/GPH/1', () => (up ? json(board()) : Promise.reject(new TypeError('network'))))
    await screen.findByText('08:38')
    up = false
    await pass(31_000)
    await screen.findByText('Live updates unavailable')

    up = true
    await pass(10_000)

    await waitFor(() => expect(screen.queryByText('Live updates unavailable')).not.toBeInTheDocument())
    expect(screen.queryByText('Last known arrival')).not.toBeInTheDocument()
    expect(screen.getByText('08:38')).not.toHaveClass('text-muted')
  })

  it('moves to the new time within one poll after a delay is reported (NFR-04)', async () => {
    let current = board()
    renderDisplay('/display/platform/GPH/1', () => json(current))
    await screen.findByText('08:38')

    current = board({ trains: [call({ arrival: { scheduled: '2026-10-15T03:00:00Z', estimated: '2026-10-15T03:12:00Z' }, delayMinutes: 12 })] })
    await pass(10_000)

    expect(await screen.findByText('08:42')).toHaveClass('text-level1-time')
    expect(screen.getByText('Delayed 12 min')).toBeInTheDocument()
  })

  it('says so when the very first try fails and there is nothing to show', async () => {
    renderDisplay('/display/platform/GPH/1', () => Promise.reject(new TypeError('network')))

    await pass(31_000)

    expect(await screen.findByText('Live updates unavailable')).toBeInTheDocument()
    expect(document.querySelectorAll('.text-level1-time')).toHaveLength(0)
  })
})

describe('Main board', () => {
  const rows = [
    call({ trainNumber: '8058', platform: '1' }),
    call({
      trainNumber: '8061', platform: '1', live: false, statusKey: 'Scheduled', delayMinutes: null,
      destination: { code: 'CF', name: { en: 'Maradana', si: 'මරදාන', ta: 'மருதானை' } },
      arrival: { scheduled: '2026-10-15T03:25:00Z', estimated: null },
    }),
  ]

  it('lists each train with time, destination in three languages, platform and status (AC-E09-01 1)', async () => {
    renderDisplay('/display/board/GPH', () => json(board({ trains: rows })))

    const list = within(await screen.findByRole('list'))
    const items = list.getAllByRole('listitem')
    expect(items).toHaveLength(2)
    const first = within(items[0]!)
    expect(first.getByText('08:38')).toBeInTheDocument()
    expect(first.getByText('Colombo Fort')).toBeInTheDocument()
    expect(first.getByText('කොළඹ කොටුව')).toBeInTheDocument()
    expect(first.getByText('கொழும்பு கோட்டை')).toBeInTheDocument()
    expect(first.getByText('1')).toBeInTheDocument() // platform
    expect(first.getByText('Delayed 8 min')).toBeInTheDocument()
    const second = within(items[1]!)
    expect(second.getByText('08:55')).toBeInTheDocument() // scheduled, no estimate
    expect(second.getByText('Scheduled')).toBeInTheDocument()
  })

  it('shows a dash for a train with no platform', async () => {
    renderDisplay('/display/board/GPH', () => json(board({ trains: [call({ platform: null })] })))

    const row = within(await screen.findByRole('listitem'))
    expect(row.getByText('–')).toBeInTheDocument()
  })

  it('offline shows the scheduled times, marked "Not live", not the last estimates (AC-E09-01 4, EC-111)', async () => {
    let up = true
    renderDisplay('/display/board/GPH', () => (up ? json(board({ trains: rows })) : Promise.reject(new TypeError('network'))))
    await screen.findByText('08:38')

    up = false
    await pass(31_000)

    await screen.findByText('Live updates unavailable')
    const first = within(screen.getAllByRole('listitem')[0]!)
    expect(first.getByText('08:30')).toBeInTheDocument() // scheduled, not 08:38
    expect(first.queryByText('08:38')).not.toBeInTheDocument()
    expect(first.getByText('Not live')).toBeInTheDocument()
    expect(first.getByText('1')).toBeInTheDocument() // the platform stays
  })

  it('says what is next when nothing is due', async () => {
    renderDisplay('/display/board/GPH', () => json(board({ trains: [], next: call({ live: false, arrival: { scheduled: '2026-10-15T03:55:00Z', estimated: null } }) })))

    expect(await screen.findByText('No trains until 09:25')).toBeInTheDocument()
  })

  it('is not set up for an unknown station', async () => {
    renderDisplay('/display/board/XXX', () => json({ reason: 'station_not_found' }, 404))

    expect(await screen.findByText('This display is not set up')).toBeInTheDocument()
  })
})

describe('Display routes and the controller routes (spec 09 rule 14)', () => {
  it('leaves the controller routes behind the sign-in', async () => {
    renderDisplay('/exceptions', () => json({}))

    expect(await screen.findByRole('button', { name: 'Sign in with staff account' })).toBeInTheDocument()
    expect(useAuth).toHaveBeenCalled()
  })
})
