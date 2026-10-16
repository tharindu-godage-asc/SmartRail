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

/** 14:02:10 in Sri Lanka time (the railway's time, whatever the laptop's zone). */
export const formatClock = (at: Date | string | number): string => clock.format(new Date(at))

/** 14:02 */
export const formatClockShort = (at: Date | string | number): string => clockShort.format(new Date(at))

/** Thu 15 Oct · 14:02 */
export const formatDayAndTime = (at: Date | string | number): string => dayAndTime.format(new Date(at)).replace(',', ' ·')

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
