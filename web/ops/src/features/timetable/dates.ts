import { t } from '../../core/strings'

/** "Wed 7 Oct" from a YYYY-MM-DD date. The date is a calendar date, so the viewer's time zone does not move it. */
export function formatDate(date: string): string {
  const [y, m, d] = date.split('-').map(Number)
  if (!y || !m || !d) return date
  return new Intl.DateTimeFormat('en-GB', { timeZone: 'UTC', weekday: 'short', day: 'numeric', month: 'short' }).format(new Date(Date.UTC(y, m - 1, d))).replace(',', '')
}

export function formatRange(from: string, to: string | null): string {
  if (to === null) return t.timetable.rangeFrom(formatDate(from))
  return to === from ? formatDate(from) : t.timetable.rangeBetween(formatDate(from), formatDate(to))
}
