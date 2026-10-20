const zone = 'Asia/Colombo'

const clock = new Intl.DateTimeFormat('en-GB', { timeZone: zone, hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' })
const clockShort = new Intl.DateTimeFormat('en-GB', { timeZone: zone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
const dayAndTime = new Intl.DateTimeFormat('en-GB', {
  timeZone: zone,
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

const dateNumeric = new Intl.DateTimeFormat('en-GB', { timeZone: zone, day: '2-digit', month: '2-digit', year: 'numeric' })

/** 20/10/2026 in Sri Lanka time: digits only, so it reads the same in every language. */
export const formatDate = (at: Date | string | number): string => dateNumeric.format(new Date(at))

/** 14:02:10 in Sri Lanka time (the railway's time, whatever the laptop's zone). */
export const formatClock = (at: Date | string | number): string => clock.format(new Date(at))

/** 14:02 */
export const formatClockShort = (at: Date | string | number): string => clockShort.format(new Date(at))

/** Thu 15 Oct · 14:02 */
export const formatDayAndTime = (at: Date | string | number): string => dayAndTime.format(new Date(at)).replace(',', ' ·')

/**
 * A length of time in whole minutes as people say it: "45 min", "4 h", "1 h 05 min". Used for delays, so 240 is "4 h", never "240 min".
 * Rounded to whole minutes, never negative.
 */
export function formatMinutes(total: number): string {
  const minutes = Math.max(0, Math.round(total))
  if (minutes < 60) {
    return `${minutes} min`
  }

  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return rest === 0 ? `${hours} h` : `${hours} h ${String(rest).padStart(2, '0')} min`
}

/** "45 s", "3 min", "1 h 05 min". Rounded down, never negative. */
export function formatAge(seconds: number): string {
  const total = Math.max(0, Math.floor(seconds))
  if (total < 60) {
    return `${total} s`
  }

  const minutes = Math.floor(total / 60)
  if (minutes < 60) {
    return `${minutes} min`
  }

  return `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')} min`
}

const dayKey = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' })

/** The Sri Lanka calendar day of an instant, as 2026-10-16. */
export const colomboDate = (at: Date | string | number): string => dayKey.format(new Date(at))

const colomboDay = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' })

/** Today's calendar date in Sri Lanka as YYYY-MM-DD (the railway's day, whatever the laptop's zone). */
export const todayInColombo = (at: Date | number = Date.now()): string => colomboDay.format(at)

/** The date after a YYYY-MM-DD date, as YYYY-MM-DD. */
export function nextDay(date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  return new Date(Date.UTC(y ?? 1970, (m ?? 1) - 1, (d ?? 1) + 1)).toISOString().slice(0, 10)
}
