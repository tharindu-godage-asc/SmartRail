import { useEffect, useRef, useState } from 'react'
import type { ExceptionItem } from '../../core/api/types'
import { t } from '../../core/strings'

export interface Seen {
  ids: ReadonlySet<string>
  count: number
}

/**
 * What a screen reader should hear after a refresh (NFR-32): only a meaning change. A new Critical exception, or the open count
 * going up. A refresh that changes nothing, or a count going down, says nothing. The first load says nothing either.
 */
export function announcementFor(before: Seen | null, items: ExceptionItem[], count: number): string | null {
  if (before === null) {
    return null
  }

  const fresh = items.find((item) => item.severity === 'Critical' && !before.ids.has(item.id))
  if (fresh) {
    return t.announce.newCritical(fresh.trainNumber)
  }

  return count > before.count ? t.announce.count(count) : null
}

/** The text for the page's polite live region; it changes only when `announcementFor` has something to say. */
export function useAnnouncement(items: ExceptionItem[] | undefined, count: number | undefined): string {
  const seen = useRef<Seen | null>(null)
  const [message, setMessage] = useState('')

  useEffect(() => {
    if (items === undefined || count === undefined) {
      return
    }

    const next = announcementFor(seen.current, items, count)
    seen.current = { ids: new Set(items.map((item) => item.id)), count }
    if (next !== null) {
      setMessage(next)
    }
  }, [items, count])

  return message
}
