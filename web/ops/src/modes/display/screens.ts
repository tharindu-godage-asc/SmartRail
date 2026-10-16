import { colomboDate } from '../../core/time'
import { d } from './displayStrings'
import type { Board, BoardCall } from './types'

/**
 * What a platform display shows (discovery 19.5, spec 09 rules 3, 4, 8, 9). Decided from the board only: the browser calculates no ETA.
 * - `train`: the next train on the platform. `due`: its expected time is the largest element. `atPlatform`: it has arrived and not left.
 *   `unknown`: its delay length is not known, so no time is given ("Waiting for update").
 * - `noTrain`: nothing is due, and the next service today is named. `endOfDay`: nothing more today, with the first service on the next
 *   day a service runs (or nothing when there is none in the horizon).
 */
export type PlatformScreen =
  | { kind: 'train'; mode: 'due' | 'atPlatform' | 'unknown'; call: BoardCall }
  | { kind: 'noTrain'; next: BoardCall }
  | { kind: 'endOfDay'; firstTomorrow: BoardCall | null }

export function platformScreen(board: Board): PlatformScreen {
  const call = board.trains[0]
  if (call) {
    // At the platform wins over an unknown delay: the train is there, whatever else is not known.
    const mode = call.state === 'AtPlatform' ? 'atPlatform' : call.delayKnown ? 'due' : 'unknown'
    return { kind: 'train', mode, call }
  }

  return board.next ? { kind: 'noTrain', next: board.next } : { kind: 'endOfDay', firstTomorrow: board.firstTomorrow }
}

/** The trains after the first one on the platform, in the order they are expected (spec 09 section 16). Empty when there is no first train. */
export function upcomingOf(board: Board): BoardCall[] {
  return board.trains.slice(1)
}

/** How many upcoming trains are shown at a time, and how long each page stays (seconds). */
export const UPCOMING_PAGE_SIZE = 3
export const UPCOMING_PAGE_SECONDS = 8

/** Splits the upcoming trains into pages of `size`; the last page may be shorter. */
export function pagesOf<T>(items: T[], size: number = UPCOMING_PAGE_SIZE): T[][] {
  const pages: T[][] = []
  for (let start = 0; start < items.length; start += size) {
    pages.push(items.slice(start, start + size))
  }

  return pages
}

/** Level 1: when the train is expected at this station. The first stop has no arrival, so its departure is given (and labelled so). */
export function expectedTime(call: BoardCall): { at: string; kind: 'arrival' | 'departure'; scheduled: string } | null {
  if (call.arrival) {
    return { at: call.arrival.estimated ?? call.arrival.scheduled, kind: 'arrival', scheduled: call.arrival.scheduled }
  }

  if (call.departure) {
    return { at: call.departure.estimated ?? call.departure.scheduled, kind: 'departure', scheduled: call.departure.scheduled }
  }

  return null
}

export type StatusKind = 'delayed' | 'onTime' | 'scheduled' | 'atPlatform' | 'waiting'
export type StatusGroupName = 'normal' | 'warning' | 'unknown'

/** The words and the group of a call's status. Never colour alone: the screens put an icon and words on every one (NFR-30). */
export function statusOf(call: BoardCall): { kind: StatusKind; group: StatusGroupName } {
  if (call.state === 'AtPlatform') return { kind: 'atPlatform', group: 'normal' }
  if (!call.delayKnown) return { kind: 'waiting', group: 'unknown' }
  if (call.statusKey === 'Delayed') return { kind: 'delayed', group: 'warning' }
  if (call.statusKey === 'OnTime') return { kind: 'onTime', group: 'normal' }
  return { kind: 'scheduled', group: 'normal' }
}

/** True when the call is on the day after the board was made (Sri Lanka days), so "tomorrow" is the right word. */
export function isTomorrow(callTime: string, serverTime: string): boolean {
  const next = new Date(new Date(serverTime).getTime() + 24 * 60 * 60 * 1000)
  return colomboDate(callTime) === colomboDate(next)
}

/** The English status words for a chip. The three-language wording is shown next to it on the platform display. */
export function statusText(kind: StatusKind, delayMinutes: number | null): string {
  switch (kind) {
    case 'delayed':
      return d.delayed(delayMinutes ?? 0).en
    case 'onTime':
      return d.onTime.en
    case 'atPlatform':
      return d.atPlatform.en
    case 'waiting':
      return d.waiting.en
    default:
      return d.scheduled.en
  }
}
