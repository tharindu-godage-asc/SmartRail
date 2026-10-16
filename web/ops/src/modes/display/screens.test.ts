import { describe, expect, it } from 'vitest'
import { allTexts, d } from './displayStrings'
import { expectedTime, isTomorrow, platformScreen, statusOf, statusText } from './screens'
import { board, call } from './fixtures'

describe('platformScreen (what a platform display shows)', () => {
  it('shows the next train as due, with its expected time as Level 1', () => {
    const screen = platformScreen(board())

    expect(screen).toMatchObject({ kind: 'train', mode: 'due' })
  })

  it('shows "waiting for update" when the length of the delay is not known', () => {
    const screen = platformScreen(board({ trains: [call({ delayKnown: false, arrival: { scheduled: '2026-10-15T03:00:00Z', estimated: null } })] }))

    expect(screen).toMatchObject({ kind: 'train', mode: 'unknown' })
  })

  it('shows at platform for a train that has arrived, even when its delay was reported unknown', () => {
    const screen = platformScreen(board({ trains: [call({ state: 'AtPlatform', delayKnown: false })] }))

    expect(screen).toMatchObject({ kind: 'train', mode: 'atPlatform' })
  })

  it('shows only the first train when two are due (FR-81)', () => {
    const screen = platformScreen(board({ trains: [call({ trainNumber: '8058' }), call({ trainNumber: '8061' })] }))

    expect(screen.kind === 'train' && screen.call.trainNumber).toBe('8058')
  })

  it('names the next service today when nothing is due (D-45)', () => {
    const screen = platformScreen(board({ trains: [], next: call({ live: false, trainNumber: '8062' }) }))

    expect(screen).toMatchObject({ kind: 'noTrain', next: { trainNumber: '8062' } })
  })

  it('says end of day when nothing is next, with the first service of the next day if there is one', () => {
    expect(platformScreen(board({ trains: [] }))).toEqual({ kind: 'endOfDay', firstTomorrow: null })
    expect(platformScreen(board({ trains: [], firstTomorrow: call({ trainNumber: '8061' }) }))).toMatchObject({
      kind: 'endOfDay',
      firstTomorrow: { trainNumber: '8061' },
    })
  })
})

describe('expectedTime (Level 1)', () => {
  it('is the estimated arrival, or the scheduled one when there is no estimate', () => {
    expect(expectedTime(call())).toEqual({ at: '2026-10-15T03:08:00Z', kind: 'arrival', scheduled: '2026-10-15T03:00:00Z' })
    expect(expectedTime(call({ arrival: { scheduled: '2026-10-15T03:00:00Z', estimated: null } }))?.at).toBe('2026-10-15T03:00:00Z')
  })

  it('is the departure at the first stop, which has no arrival, and says so', () => {
    expect(expectedTime(call({ arrival: null }))).toMatchObject({ kind: 'departure', at: '2026-10-15T03:09:00Z' })
  })

  it('is nothing when the call has neither', () => {
    expect(expectedTime(call({ arrival: null, departure: null }))).toBeNull()
  })
})

describe('statusOf (never colour alone: every kind has words)', () => {
  it.each([
    [{ state: 'AtPlatform' }, 'atPlatform', 'normal'],
    [{ delayKnown: false }, 'waiting', 'unknown'],
    [{ statusKey: 'Delayed' }, 'delayed', 'warning'],
    [{ statusKey: 'OnTime', delayMinutes: 0 }, 'onTime', 'normal'],
    [{ statusKey: 'Scheduled', live: false, delayMinutes: null }, 'scheduled', 'normal'],
  ])('%j is %s in the %s group', (overrides, kind, group) => {
    expect(statusOf(call(overrides))).toEqual({ kind, group })
  })

  it('has English words for every kind', () => {
    expect(statusText('delayed', 8)).toBe('Delayed 8 min')
    expect(statusText('onTime', 0)).toBe('On time')
    expect(statusText('scheduled', null)).toBe('Scheduled')
    expect(statusText('atPlatform', 0)).toBe('At platform')
    expect(statusText('waiting', 0)).toBe('Waiting for update')
  })
})

describe('isTomorrow', () => {
  it('compares Sri Lanka days, not UTC days', () => {
    // 22:30 UTC on the 15th is 04:00 on the 16th in Sri Lanka, so a call at 02:00 UTC on the 16th (07:30) is the same day, not tomorrow.
    expect(isTomorrow('2026-10-16T02:00:00Z', '2026-10-15T22:30:00Z')).toBe(false)
    expect(isTomorrow('2026-10-17T02:00:00Z', '2026-10-15T22:30:00Z')).toBe(true)
  })
})

describe('three languages (D-19, FR-83)', () => {
  it('has English, Sinhala and Tamil for every text, never an empty one', () => {
    for (const { path, text } of allTexts()) {
      expect(text.en, `${path} en`).not.toBe('')
      expect(text.si, `${path} si`).not.toBe('')
      expect(text.ta, `${path} ta`).not.toBe('')
    }
  })

  it('carries the number or the time of a text into all three languages', () => {
    for (const text of [d.delayed(7), d.aboutLate(7)]) {
      expect(text.si).toContain('7')
      expect(text.ta).toContain('7')
    }
    for (const text of [d.noTrainsUntil('09:25')]) {
      expect(text.si).toContain('09:25')
      expect(text.ta).toContain('09:25')
    }
    expect(d.firstTomorrow('05:30').si).toContain('05:30')
    expect(d.firstTomorrow('05:30').ta).toContain('05:30')
  })

  it('says a delay in hours and minutes once it is an hour or more, in all three languages', () => {
    expect(d.delayed(45)).toEqual({ en: 'Delayed 45 min', si: 'මිනිත්තු 45ක් ප්‍රමාදයි', ta: '45 நிமிடம் தாமதம்' })
    expect(d.delayed(240)).toEqual({ en: 'Delayed 4 h', si: 'පැය 4ක් ප්‍රමාදයි', ta: '4 மணி தாமதம்' })
    expect(d.delayed(65)).toEqual({ en: 'Delayed 1 h 05 min', si: 'පැය 1ක් මිනිත්තු 5ක් ප්‍රමාදයි', ta: '1 மணி 5 நிமிடம் தாமதம்' })
    expect(d.aboutLate(240)).toEqual({ en: 'About 4 h late', si: 'පැය 4ක් පමණ ප්‍රමාදයි', ta: 'சுமார் 4 மணி தாமதம்' })
    expect(d.aboutLate(10).en).toBe('About 10 min late')
    expect(statusText('delayed', 240)).toBe('Delayed 4 h')
    expect(statusText('delayed', 90)).toBe('Delayed 1 h 30 min')
  })

  it('has no text that is only English copied into the other two', () => {
    for (const { path, text } of allTexts()) {
      expect(text.si, path).not.toBe(text.en)
      expect(text.ta, path).not.toBe(text.en)
    }
  })

  it('keeps the phrases taken from the design canvas exactly as drawn', () => {
    expect(d.liveUnavailable).toEqual({ en: 'Live updates unavailable', si: 'සජීවී යාවත්කාලීන නොමැත', ta: 'நேரடி புதுப்பிப்புகள் இல்லை' })
    expect(d.expectedArrival.si).toBe('අපේක්ෂිත පැමිණීම')
    expect(d.noNews.ta).toBe('செய்தி இல்லை')
  })
})
