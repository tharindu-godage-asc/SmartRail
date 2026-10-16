import { describe, expect, it, vi } from 'vitest'
import { ApiError, createApi } from './api/client'
import { freshnessOf } from './freshness'
import { exceptionTypeLabel, journeyStatusLabel, severityLabel, sourceLabel } from './strings'
import { formatAge, formatClock, formatClockShort, formatMinutes } from './time'
import { rolesOf, isController } from '../features/auth/roles'
import { announcementFor } from '../features/exceptions/announcements'
import { exception, makeToken } from '../test/helpers'

describe('time', () => {
  it('shows the railway time in Sri Lanka whatever the laptop zone is', () => {
    expect(formatClock('2026-10-15T08:32:10Z')).toBe('14:02:10')
    expect(formatClockShort('2026-10-15T08:32:10Z')).toBe('14:02')
  })

  it.each([
    [0, '0 s'],
    [45, '45 s'],
    [59.9, '59 s'],
    [60, '1 min'],
    [180, '3 min'],
    [3599, '59 min'],
    [3900, '1 h 05 min'],
    [-5, '0 s'],
  ])('formats an age of %s seconds as %s', (seconds, text) => {
    expect(formatAge(seconds)).toBe(text)
  })
})

describe('formatMinutes (delays in hours and minutes, never 240 min)', () => {
  it.each([
    [0, '0 min'],
    [8, '8 min'],
    [59, '59 min'],
    [60, '1 h'],
    [65, '1 h 05 min'],
    [90, '1 h 30 min'],
    [240, '4 h'],
    [245.4, '4 h 05 min'],
    [-3, '0 min'],
  ])('formats %s minutes as %s', (minutes, text) => {
    expect(formatMinutes(minutes)).toBe(text)
  })
})

describe('roles', () => {
  it('reads the flat roles claim of an access token', () => {
    expect(rolesOf(makeToken({ roles: ['controller', 'other'] }))).toEqual(['controller', 'other'])
    expect(isController(makeToken({ roles: ['controller'] }))).toBe(true)
  })

  it('treats a missing claim, an operator, a broken token and no token as not a controller', () => {
    expect(isController(makeToken({}))).toBe(false)
    expect(isController(makeToken({ roles: ['operator'] }))).toBe(false)
    expect(isController(makeToken({ roles: 'controller' }))).toBe(false)
    expect(isController('not.a.jwt')).toBe(false)
    expect(isController('nonsense')).toBe(false)
    expect(isController(undefined)).toBe(false)
  })
})

describe('labels', () => {
  it('words the known codes', () => {
    expect(exceptionTypeLabel('ReasonAwaitingVerification')).toBe('Delay reason waiting for verification')
    expect(exceptionTypeLabel('EarlyDeparture')).toBe('Departed before scheduled time')
    expect(severityLabel('Critical')).toBe('Critical')
    expect(sourceLabel('OperatorReport')).toBe('Operator report')
  })

  it('shows an unknown code as it is, so a new type never breaks the screen', () => {
    expect(exceptionTypeLabel('PlatformConflict')).toBe('PlatformConflict')
    expect(severityLabel('Low')).toBe('Low')
    expect(sourceLabel('Satellite')).toBe('Satellite')
  })

  it('says "Delay unknown" and never a number when the length is not known', () => {
    expect(journeyStatusLabel({ statusKey: 'Delayed', delayKnown: true, delayMinutes: 6 })).toBe('Delayed 6 min')
    expect(journeyStatusLabel({ statusKey: 'Delayed', delayKnown: true, delayMinutes: 240 })).toBe('Delayed 4 h')
    expect(journeyStatusLabel({ statusKey: 'Delayed', delayKnown: true, delayMinutes: 75 })).toBe('Delayed 1 h 15 min')
    expect(journeyStatusLabel({ statusKey: 'Delayed', delayKnown: false, delayMinutes: 6 })).toBe('Delay unknown')
    expect(journeyStatusLabel({ statusKey: 'OnTime', delayKnown: true, delayMinutes: 0 })).toBe('On time')
    expect(journeyStatusLabel({ statusKey: 'Suspended', delayKnown: true, delayMinutes: 0 })).toBe('Suspended')
  })
})

describe('freshness (NFR-04)', () => {
  const stamp = (dataUpdatedAt: number, isError = false) => ({ dataUpdatedAt, isError })

  it('is fresh inside 30 seconds and stale after', () => {
    expect(freshnessOf([stamp(1000)], 31_000, 30_000).stale).toBe(false)
    expect(freshnessOf([stamp(1000)], 31_001, 30_000).stale).toBe(true)
  })

  it('goes by the oldest of the answers and reports its time', () => {
    const result = freshnessOf([stamp(10_000), stamp(50_000)], 60_000, 30_000)
    expect(result.stale).toBe(true)
    expect(result.lastGood).toBe(10_000)
  })

  it('says "never loaded" only when nothing has arrived and the try failed', () => {
    expect(freshnessOf([stamp(0, true)], 5000, 30_000)).toEqual({ stale: false, neverLoaded: true, lastGood: null })
    expect(freshnessOf([stamp(0, false)], 5000, 30_000).neverLoaded).toBe(false)
  })
})

describe('announcements (NFR-32)', () => {
  const before = { ids: new Set(['e1']), count: 1 }

  it('says nothing on the first load', () => {
    expect(announcementFor(null, [exception()], 1)).toBeNull()
  })

  it('says nothing when a refresh changes nothing', () => {
    expect(announcementFor(before, [exception()], 1)).toBeNull()
  })

  it('announces a new Critical exception', () => {
    expect(announcementFor(before, [exception(), exception({ id: 'e2', trainNumber: '8065' })], 2)).toBe('New critical exception for train 8065')
  })

  it('announces a rising count for a High one, and nothing when the count falls', () => {
    expect(announcementFor(before, [exception(), exception({ id: 'e2', severity: 'High' })], 2)).toBe('Open exceptions: 2')
    expect(announcementFor({ ids: new Set(['e1', 'e2']), count: 2 }, [exception()], 1)).toBeNull()
  })
})

describe('API client', () => {
  const answer = (status: number, body?: unknown) => new Response(body === undefined ? null : JSON.stringify(body), { status })

  function setup(responses: Response[], renewed?: string) {
    const fetchFn = vi.fn<typeof fetch>()
    responses.forEach((response) => fetchFn.mockResolvedValueOnce(response))
    const onBlocked = vi.fn()
    const renew = vi.fn(async () => renewed)
    const api = createApi({ baseUrl: 'http://api', getToken: () => 'token-1', renew, onBlocked, fetchFn })
    return { api, fetchFn, onBlocked, renew }
  }

  it('sends the token in the Authorization header and never in the address', async () => {
    const { api, fetchFn } = setup([answer(200, { ok: true })])

    await api.get('/api/v1/overview')

    const [url, init] = fetchFn.mock.calls[0]!
    expect(url).toBe('http://api/api/v1/overview')
    expect(String(url)).not.toContain('token-1')
    expect((init!.headers as Record<string, string>).Authorization).toBe('Bearer token-1')
  })

  it('renews once on a 401 and repeats the request with the new token', async () => {
    const { api, fetchFn, renew, onBlocked } = setup([answer(401), answer(200, { ok: 1 })], 'token-2')

    await expect(api.get('/x')).resolves.toEqual({ ok: 1 })

    expect(renew).toHaveBeenCalledTimes(1)
    expect((fetchFn.mock.calls[1]![1]!.headers as Record<string, string>).Authorization).toBe('Bearer token-2')
    expect(onBlocked).not.toHaveBeenCalled()
  })

  it('says the session ended when the 401 cannot be renewed', async () => {
    const { api, onBlocked } = setup([answer(401)], undefined)

    await expect(api.get('/x')).rejects.toMatchObject({ status: 401 })
    expect(onBlocked).toHaveBeenCalledWith('session-ended')
  })

  it('says forbidden on a 403 and keeps the reason code of an error body', async () => {
    const { api, onBlocked } = setup([answer(403, { reason: 'no_subject' })])

    await expect(api.get('/x')).rejects.toMatchObject({ status: 403, reason: 'no_subject' })
    expect(onBlocked).toHaveBeenCalledWith('forbidden')
  })

  it('turns a 409 with a reason into an ApiError the screen can word', async () => {
    const { api, onBlocked } = setup([answer(409, { reason: 'already_resolved' })])

    const error = await api.post('/x', { action: 'Escalated' }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, reason: 'already_resolved' })
    expect(onBlocked).not.toHaveBeenCalled()
  })

  it('sends nothing when there is no token', async () => {
    const fetchFn = vi.fn<typeof fetch>()
    const onBlocked = vi.fn()
    const api = createApi({ baseUrl: 'http://api', getToken: () => undefined, renew: async () => undefined, onBlocked, fetchFn })

    await expect(api.get('/x')).rejects.toMatchObject({ status: 401 })
    expect(fetchFn).not.toHaveBeenCalled()
  })
})
