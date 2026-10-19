import { describe, expect, it } from 'vitest'
import type { ValidationIssue } from '../../../core/api/types'
import { options } from '../fixtures'
import { addMinute, detailsProblems, includedStops, initialDraft, inRunningOrder, isLocked, reduce, stopsProblems, toRequest, type Draft } from './draft'
import { issuesForStop, placeOf } from './issues'

const weekly = (): Draft => initialDraft('Weekly', options, '2026-10-20')
const filled = (): Draft => {
  let d = weekly()
  d = reduce(d, { type: 'set', field: 'trainNumber', value: ' 8072 ' })
  for (const day of ['Fri', 'Mon', 'Tue']) d = reduce(d, { type: 'toggleDay', day })
  d = reduce(d, { type: 'time', stationCode: 'VYG', value: '17:25' })
  d = reduce(d, { type: 'time', stationCode: 'GPH', value: '17:35' })
  d = reduce(d, { type: 'time', stationCode: 'RGM', value: '17:45' })
  d = reduce(d, { type: 'time', stationCode: 'MDA', value: '17:52' })
  d = reduce(d, { type: 'time', stationCode: 'CF', value: '17:58' })
  return d
}

describe('the draft', () => {
  it('starts with every station of the line in line order and runs inbound, toward the first station', () => {
    const d = weekly()

    expect(d.stops.map((s) => s.stationCode)).toEqual(['CF', 'MDA', 'RGM', 'GPH', 'VYG'])
    expect(inRunningOrder(d).map((s) => s.stationCode)).toEqual(['VYG', 'GPH', 'RGM', 'MDA', 'CF'])
    expect(inRunningOrder(reduce(d, { type: 'direction', value: 'outbound' })).map((s) => s.stationCode)).toEqual(['CF', 'MDA', 'RGM', 'GPH', 'VYG'])
  })

  it('locks the first and last stop of the run and lets the others be left out in Choose stops', () => {
    let d = reduce(weekly(), { type: 'stopMode', mode: 'choose' })

    expect(isLocked(d, 'VYG')).toBe(true)
    expect(isLocked(d, 'CF')).toBe(true)
    expect(isLocked(d, 'RGM')).toBe(false)
    d = reduce(d, { type: 'toggleStop', stationCode: 'VYG' }) // refused: locked
    d = reduce(d, { type: 'toggleStop', stationCode: 'RGM' })
    expect(includedStops(d).map((s) => s.stationCode)).toEqual(['VYG', 'GPH', 'MDA', 'CF'])
  })

  it('"Every stop" includes the whole line again', () => {
    let d = reduce(weekly(), { type: 'stopMode', mode: 'choose' })
    d = reduce(d, { type: 'toggleStop', stationCode: 'RGM' })

    d = reduce(d, { type: 'stopMode', mode: 'every' })

    expect(includedStops(d)).toHaveLength(5)
  })

  it('keeps a list of dates sorted without repeats and lets one be removed', () => {
    let d = initialDraft('Extra', options, '2026-10-20')
    for (const date of ['2026-10-23', '2026-10-21', '2026-10-23', '']) d = reduce(d, { type: 'addDate', date })

    expect(d.dates).toEqual(['2026-10-21', '2026-10-23'])
    expect(reduce(d, { type: 'removeDate', date: '2026-10-21' }).dates).toEqual(['2026-10-23'])
  })
})

describe('what the screen blocks itself (spec 15 rule 1)', () => {
  it('needs a train number, a day and a date for a weekly service, and dates for an extra one', () => {
    expect(detailsProblems({ ...weekly(), effectiveFrom: '' })).toHaveLength(3)
    expect(detailsProblems(filled())).toEqual([])
    expect(detailsProblems(initialDraft('Extra', options, '2026-10-20'))).toHaveLength(2) // number, dates
  })

  it('needs a time at every included stop and at least two stops', () => {
    expect(stopsProblems(weekly())).toHaveLength(1) // times missing
    expect(stopsProblems(filled())).toEqual([])
    const bad = reduce(filled(), { type: 'time', stationCode: 'GPH', value: '25:00' })
    expect(stopsProblems(bad)).toHaveLength(1)
  })
})

describe('the request (spec 15 rule 5 and section 5)', () => {
  it('sends a weekly service: first stop departure, middle stops arrival and a departure 1 minute later, last stop arrival', () => {
    const request = toRequest(filled())

    expect(request).toMatchObject({
      kind: 'Weekly',
      trainNumber: '8072',
      lineCode: 'MAIN',
      category: 'Regional',
      serviceType: 'Slow',
      days: ['Mon', 'Tue', 'Fri'],
      effectiveFrom: '2026-10-20',
    })
    expect(request.stops).toEqual([
      { stationCode: 'VYG', arrival: null, departure: '17:25', platform: null },
      { stationCode: 'GPH', arrival: '17:35', departure: '17:36', platform: null },
      { stationCode: 'RGM', arrival: '17:45', departure: '17:46', platform: null },
      { stationCode: 'MDA', arrival: '17:52', departure: '17:53', platform: null },
      { stationCode: 'CF', arrival: '17:58', departure: null, platform: null },
    ])
    expect('acknowledgeWarnings' in request).toBe(false)
  })

  it('leaves out the stops the train does not make, and sends the platforms and the confirmation of advice', () => {
    let d = reduce(filled(), { type: 'stopMode', mode: 'choose' })
    d = reduce(d, { type: 'toggleStop', stationCode: 'RGM' })
    d = reduce(d, { type: 'platform', stationCode: 'GPH', value: '2' })

    const request = toRequest(d, true)

    expect(request.stops.map((s) => s.stationCode)).toEqual(['VYG', 'GPH', 'MDA', 'CF'])
    expect(request.stops[1]!.platform).toBe('2')
    expect(request.acknowledgeWarnings).toBe(true)
  })

  it('sends an extra train with its dates and no days', () => {
    let d = initialDraft('Extra', options, '2026-10-20')
    d = reduce(d, { type: 'set', field: 'trainNumber', value: '8099' })
    d = reduce(d, { type: 'addDate', date: '2026-10-22' })
    d = reduce(d, { type: 'addDate', date: '2026-10-21' })

    const request = toRequest(d)

    expect(request).toMatchObject({ kind: 'Extra', dates: ['2026-10-21', '2026-10-22'] })
    expect('days' in request).toBe(false)
    expect('effectiveFrom' in request).toBe(false)
  })

  it('sends an outbound service from the line start to the line end', () => {
    const d = reduce(filled(), { type: 'direction', value: 'outbound' })

    expect(toRequest(d).stops.map((s) => s.stationCode)).toEqual(['CF', 'MDA', 'RGM', 'GPH', 'VYG'])
  })

  it('adds a minute across an hour boundary', () => {
    expect(addMinute('08:59')).toBe('09:00')
    expect(addMinute('17:35')).toBe('17:36')
    expect(addMinute('23:59')).toBe('00:00') // past midnight: the server refuses it as not increasing
  })
})

describe('where an issue belongs (spec 15 rule 2)', () => {
  const issue = (field: string): ValidationIssue => ({ code: 'x', field, message: 'm' })

  it('turns a stop index of the request back into a station, counting only the stops that were sent', () => {
    let d = reduce(filled(), { type: 'stopMode', mode: 'choose' })
    d = reduce(d, { type: 'toggleStop', stationCode: 'RGM' }) // not sent, so index 2 is Maradana, not Ragama

    expect(placeOf(issue('stops[2].arrival'), d)).toEqual({ where: 'stop', stationCode: 'MDA', field: 'time' })
    expect(placeOf(issue('stops[1].platform'), d)).toEqual({ where: 'stop', stationCode: 'GPH', field: 'platform' })
    expect(issuesForStop([issue('stops[2].departure')], d, 'MDA')).toHaveLength(1)
    expect(issuesForStop([issue('stops[2].departure')], d, 'GPH')).toHaveLength(0)
  })

  it('puts the details fields on step 1 and anything unknown in the summary only', () => {
    const d = filled()

    expect(placeOf(issue('trainNumber'), d)).toEqual({ where: 'details', field: 'trainNumber' })
    expect(placeOf(issue('days'), d)).toEqual({ where: 'details', field: 'days' })
    expect(placeOf(issue('effectiveFrom'), d)).toEqual({ where: 'details', field: 'effectiveFrom' })
    expect(placeOf(issue('stops'), d)).toEqual({ where: 'summary' })
    expect(placeOf(issue('stops[9].arrival'), d)).toEqual({ where: 'summary' })
    expect(placeOf(issue('whatever'), d)).toEqual({ where: 'summary' })
  })
})
