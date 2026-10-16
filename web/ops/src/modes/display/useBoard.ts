import { ApiError } from '../../core/api/client'
import { config } from '../../core/config'
import { freshnessOf } from '../../core/freshness'
import { usePublicPolled } from '../../core/usePublicPolled'
import { useNow } from '../../core/useNow'
import type { Board } from './types'

/** `GET /board` for this station and platform. The code comes from the URL, so it is checked and encoded, never put in markup. */
export const boardPath = (station: string, platform?: string, count?: number) => {
  const query = new URLSearchParams()
  if (platform) query.set('platform', platform)
  if (count) query.set('count', String(count))
  const text = query.toString()
  return `/api/v1/stations/${encodeURIComponent(station)}/board${text ? `?${text}` : ''}`
}

export interface BoardState {
  /** The last good answer. It is kept through errors, never cleared. */
  board: Board | undefined
  /** Viewer silence (D-39): 30 s without a good answer. The data stays; the screens mark it. */
  offline: boolean
  /**
   * The time now by the server's clock, in ms: the server's time in the last answer plus the time that passed since it arrived. It lets
   * the corner clock tick every second between polls, and a wrong clock on the display's computer cannot make it wrong. Never used for an ETA.
   */
  serverNow: number | undefined
  /** Nothing has ever loaded, and the last try failed. */
  neverLoaded: boolean
  /** The server said the station or platform is unknown. */
  notSetUp: boolean
}

/** Station and platform codes are a few letters or digits; anything else is not a display that can be set up (spec 09 rule 13). */
export const validStation = (code: string | undefined): code is string => !!code && /^[A-Za-z]{2,4}$/.test(code)
export const validPlatform = (code: string | undefined): code is string => !!code && /^[A-Za-z0-9]{1,3}$/.test(code)

/**
 * Polls the board and works out whether the display is offline from when the last good answer arrived (spec 09 rule 10). Which time
 * the screen shows as "last update" is the API's `serverTime`, not the browser clock (ADR-06).
 */
export function useBoard(path: string | null): BoardState {
  const query = usePublicPolled<Board>('board', path ?? '', config.displayPollMs, path !== null)
  const now = useNow()
  const freshness = freshnessOf([query], now, config.staleAfterMs)
  const notFound = query.error instanceof ApiError && query.error.status === 404

  return {
    board: query.data,
    offline: freshness.stale || freshness.neverLoaded,
    serverNow: query.data ? Date.parse(query.data.serverTime) + Math.max(0, now - query.dataUpdatedAt) : undefined,
    neverLoaded: freshness.neverLoaded,
    notSetUp: path === null || (notFound && query.data === undefined),
  }
}
