import type { ClosedExceptions, ExceptionItem, OpenExceptions } from '../../core/api/types'
import { usePolled } from '../../core/usePolled'

export const useExceptions = () => usePolled<OpenExceptions>('exceptions', '/api/v1/exceptions?status=open')

/** Read only while the Closed tab is on screen (the hook is used by that tab alone). */
export const useClosedExceptions = () => usePolled<ClosedExceptions>('closedExceptions', '/api/v1/exceptions?status=resolved')

/** "8058 · Gampaha". A row without a station shows the train only (spec 08 rule 5). */
export const whereOf = (item: Pick<ExceptionItem, 'trainNumber' | 'stationName'>): string => (item.stationName ? `${item.trainNumber} · ${item.stationName}` : item.trainNumber)

/** The age from the server at the last poll, moved on by the time since then (ADR-06: the server value replaces it at each poll). */
export const liveAge = (item: ExceptionItem, dataUpdatedAt: number, now: number): number =>
  item.ageSeconds + Math.max(0, (now - dataUpdatedAt) / 1000)
