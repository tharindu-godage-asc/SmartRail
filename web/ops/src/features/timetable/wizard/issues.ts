import type { ValidationIssue } from '../../../core/api/types'
import { includedStops, type Draft } from './draft'

/**
 * Where an issue from the server belongs on screen (spec 15 rule 2). The server names the field of the request it checked, for example
 * `stops[2].arrival`; the index counts the stops that were sent, so it is turned back into a station here. A field this does not know
 * goes to the summary only.
 */
export type Place =
  | { where: 'stop'; stationCode: string; field: 'time' | 'platform' | 'station' }
  | { where: 'details'; field: 'trainNumber' | 'category' | 'serviceType' | 'days' | 'dates' | 'effectiveFrom' }
  | { where: 'summary' }

const detailFields = ['trainNumber', 'category', 'serviceType', 'days', 'dates', 'effectiveFrom'] as const

export function placeOf(issue: ValidationIssue, draft: Draft): Place {
  const stop = /^stops\[(\d+)\]\.(\w+)$/.exec(issue.field)
  if (stop) {
    const sent = includedStops(draft)[Number(stop[1])]
    if (!sent) return { where: 'summary' }
    const field = stop[2] === 'platform' ? 'platform' : stop[2] === 'stationCode' ? 'station' : 'time'
    return { where: 'stop', stationCode: sent.stationCode, field }
  }

  const found = detailFields.find((f) => f === issue.field)
  return found ? { where: 'details', field: found } : { where: 'summary' }
}

/** The issues for one cell of the stops table. */
export const issuesForStop = (issues: ValidationIssue[], draft: Draft, stationCode: string): ValidationIssue[] =>
  issues.filter((i) => {
    const place = placeOf(i, draft)
    return place.where === 'stop' && place.stationCode === stationCode
  })

export const issuesForDetails = (issues: ValidationIssue[], draft: Draft, field: (typeof detailFields)[number]): ValidationIssue[] =>
  issues.filter((i) => {
    const place = placeOf(i, draft)
    return place.where === 'details' && place.field === field
  })
