/** The answer of GET /api/v1/stations/{code}/board (spec 09). Times are UTC instants; the screens show them in Sri Lanka time. */

export interface LocalizedName {
  en: string
  si: string
  ta: string
}

export interface StationRef {
  code: string
  name: LocalizedName
}

export interface TimePair {
  scheduled: string
  /** Null when there is no honest estimate (the timetable only, or the delay length is unknown). */
  estimated: string | null
}

export interface LastSeen {
  /** `Departed` or `Arrived`. */
  state: string
  stationName: string
  at: string
}

export interface BoardCall {
  trainNumber: string
  serviceType: string
  operatingDate: string
  destination: StationRef
  platform: string | null
  live: boolean
  journeyId: string | null
  arrival: TimePair | null
  departure: TimePair | null
  /** `Due` or `AtPlatform`. */
  state: string
  delayMinutes: number | null
  delayKnown: boolean
  statusGroup: string
  /** `Scheduled`, `OnTime`, `Delayed` today. */
  statusKey: string
  version: number | null
  lastEventAt: string | null
  lastSeen: LastSeen | null
}

export interface Board {
  station: StationRef
  serverTime: string
  live: boolean
  trains: BoardCall[]
  next: BoardCall | null
  firstTomorrow: BoardCall | null
}
