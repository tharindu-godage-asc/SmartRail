export interface QueryStamp {
  /** When the last good answer arrived (ms since 1970), 0 when none has. */
  dataUpdatedAt: number
  isError: boolean
}

export interface Freshness {
  /** An answer exists but the newest one is older than the limit: show "Not up to date" (NFR-04). */
  stale: boolean
  /** Nothing has loaded yet and the last try failed. */
  neverLoaded: boolean
  /** The oldest of the last good answers, for "Last updated 14:02:10". */
  lastGood: number | null
}

/** Pure rule behind the banner (spec 08 rule 8): data is never cleared on an error, the age of the data is told instead. */
export function freshnessOf(stamps: QueryStamp[], now: number, staleAfterMs: number): Freshness {
  const loaded = stamps.filter((s) => s.dataUpdatedAt > 0)
  const oldest = loaded.length === 0 ? null : Math.min(...loaded.map((s) => s.dataUpdatedAt))
  return {
    stale: oldest !== null && now - oldest > staleAfterMs,
    neverLoaded: stamps.some((s) => s.dataUpdatedAt === 0 && s.isError),
    lastGood: oldest,
  }
}
