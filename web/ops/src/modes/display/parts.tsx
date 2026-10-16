import { useEffect, useState, type ReactNode } from 'react'
import { Icon, type IconName } from '../../core/Icon'
import { Wordmark } from '../../core/Wordmark'
import { formatClockShort } from '../../core/time'
import { d, type Tri } from './displayStrings'
import type { StatusGroupName } from './screens'

const WIDTH = 1920
const HEIGHT = 1080

const fit = () => Math.min(window.innerWidth / WIDTH, window.innerHeight / HEIGHT)

/**
 * The display is drawn once at 1920 by 1080 (as on the design canvas) and scaled to whatever screen it runs on, so the type scale of
 * the tokens (Level 1 at 160 px and so on) holds at the size it was designed for. The dark theme comes from the token file.
 */
export function Stage({ children }: { children: ReactNode }) {
  const [scale, setScale] = useState(fit)
  useEffect(() => {
    const onResize = () => setScale(fit())
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  return (
    <div className="theme-dark fixed inset-0 overflow-hidden bg-page text-ink">
      <div
        className="absolute flex flex-col"
        style={{ width: WIDTH, height: HEIGHT, left: '50%', top: '50%', transform: `translate(-50%, -50%) scale(${scale})` }}
      >
        {children}
      </div>
    </div>
  )
}

type Language = 'en' | 'si' | 'ta'

const scriptStyle: Record<Language, React.CSSProperties | undefined> = {
  en: undefined,
  si: { fontFamily: 'var(--font-family-sinhala)', lineHeight: 'var(--line-height-display-sinhala)' },
  ta: { fontFamily: 'var(--font-family-tamil)', lineHeight: 'var(--line-height-display-tamil)' },
}

/**
 * A text in the three languages side by side, always, never rotating (D-19, FR-83). Each span says its language, so a screen reader
 * uses the right voice. `sizes` sets a different size for one language (the canvas draws English larger in a destination).
 */
export function TriText({
  text,
  order = ['en', 'si', 'ta'],
  className = '',
  sizes = {},
}: {
  text: Tri
  order?: Language[]
  className?: string
  sizes?: Partial<Record<Language, string>>
}) {
  return (
    <span className={`flex flex-wrap items-baseline gap-x-[36px] ${className}`}>
      {order.map((language) => (
        <span key={language} lang={language} className={sizes[language] ?? ''} style={scriptStyle[language]}>
          {text[language]}
        </span>
      ))}
    </span>
  )
}

const chipLook: Record<StatusGroupName, { box: string; icon: IconName }> = {
  normal: { box: 'border-[3px] border-solid border-normal bg-normal-bg text-normal', icon: 'schedule' },
  warning: { box: 'border-[6px] border-solid border-warning bg-warning-bg text-warning', icon: 'warning' },
  unknown: { box: 'border-[3px] border-dashed border-unknown bg-unknown-bg text-unknown', icon: 'help' },
}

/** A status as icon plus words, never colour alone (NFR-30). The outline weight differs per group, as in the tokens. */
export function StatusChip({ group, text, size = 'large', icon }: { group: StatusGroupName; text: string; size?: 'large' | 'small'; icon?: IconName }) {
  const look = chipLook[group]
  const big = size === 'large'
  return (
    <span
      className={`inline-flex items-center whitespace-nowrap rounded-pill font-bold tabular-nums ${look.box} ${
        big ? 'gap-[22px] px-[28px] py-[10px] text-[56px] leading-[1.2]' : 'gap-[12px] px-[18px] py-[4px] text-[34px] leading-[1.2]'
      }`}
    >
      <Icon name={icon ?? look.icon} size={big ? 70 : 38} />
      <span>{text}</span>
    </span>
  )
}

/** Viewer silence (D-39, NFR-03): the inverted strip of the canvas, in three languages with the last time the display heard from the server. */
export function OfflineBanner({ lastUpdate }: { lastUpdate: string | null }) {
  return (
    <div role="status" className="flex items-center gap-[24px] bg-ink px-[72px] py-[20px] text-page">
      <Icon name="cloudOff" size={56} />
      <div className="flex flex-col gap-[2px]">
        <TriText text={d.liveUnavailable} className="text-level4 font-bold" />
        {lastUpdate ? (
          <span className="text-level4 tabular-nums">
            {d.lastUpdate.en} {formatClockShort(lastUpdate)}
          </span>
        ) : null}
      </div>
    </div>
  )
}

/** The footer of the canvas: the wordmark, and when the screen last heard from the server (the server's clock, never the browser's). */
export function DisplayFooter({ updatedAt, offline }: { updatedAt: string | null; offline: boolean }) {
  return (
    <footer className="flex items-center justify-between px-[72px] pb-[32px]">
      <span className="text-muted">
        <Wordmark />
      </span>
      {updatedAt ? (
        <span className="inline-flex items-center gap-[14px] text-level4 tabular-nums text-muted">
          <svg width="22" height="22" viewBox="0 0 12 12" aria-hidden="true" className="shrink-0">
            {offline ? (
              <circle cx="6" cy="6" r="4.2" fill="none" stroke="currentColor" strokeWidth="1.8" />
            ) : (
              <circle cx="6" cy="6" r="4.5" fill="currentColor" />
            )}
          </svg>
          {offline ? d.lastUpdate.en : d.updated.en} {formatClockShort(updatedAt)}
        </span>
      ) : null}
    </footer>
  )
}

/** The whole-screen messages: nothing loaded yet, or a station or platform that is not set up. */
export function Message({ text }: { text: Tri }) {
  return (
    <Stage>
      <main className="flex grow flex-col items-start justify-center gap-[24px] px-[72px]">
        <TriText text={text} className="text-level2 font-bold" order={['en', 'si', 'ta']} />
      </main>
      <DisplayFooter updatedAt={null} offline={false} />
    </Stage>
  )
}
