import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import { severityLabel, t } from './strings'

/** The shared parts from the design canvas (Components-Dashboard). Status is always icon plus words, never colour alone (NFR-30). */

export function Button({
  variant = 'secondary',
  className = '',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' }) {
  const look =
    variant === 'primary'
      ? 'bg-ink text-page border-ink'
      : 'bg-page text-ink border-ink'
  return (
    <button
      type="button"
      className={`min-h-touch rounded-md border-[1.5px] px-[18px] text-[15px] font-bold disabled:cursor-not-allowed disabled:opacity-50 ${look} ${className}`}
      {...props}
    />
  )
}

/** The permanent notice (FR-63). It has no close button and is on every screen. */
export function SafetyBar() {
  return (
    <div role="note" className="flex items-center gap-[10px] bg-ink px-lg py-sm text-[13px] font-semibold text-page">
      <Icon name="info" />
      <span>{t.safetyNotice}</span>
    </div>
  )
}

export function Footer() {
  return <footer className="border-t border-line bg-page px-lg py-[14px] text-center text-[13px] text-muted">{t.footer}</footer>
}

export function Panel({ title, children, className = '' }: { title?: string; children: ReactNode; className?: string }) {
  return (
    <section className={`flex flex-col gap-[10px] rounded-lg border border-line bg-page p-md ${className}`}>
      {title ? <h2 className="m-0 text-[13px] font-bold uppercase tracking-[.6px] text-muted">{title}</h2> : null}
      {children}
    </section>
  )
}

export function KpiCard({ label, value, note }: { label: string; value: ReactNode; note?: ReactNode }) {
  return (
    <div className="flex flex-1 flex-col gap-xs rounded-lg border border-line bg-page p-md">
      <span className="text-label font-semibold text-muted">{label}</span>
      <span className="text-[32px] font-bold leading-none tabular-nums">{value}</span>
      <span className="min-h-[18px] text-caption text-muted">{note}</span>
    </div>
  )
}

/** Critical is a filled-icon pill with the disrupted colour plus the word; High is outlined with a triangle (design: SeverityBadge). */
export function SeverityBadge({ severity }: { severity: string }) {
  const critical = severity === 'Critical'
  const tone = critical ? 'border-disrupted text-disrupted' : 'border-ink text-ink'
  return (
    <span className={`inline-flex items-center gap-xs whitespace-nowrap rounded-pill border-[1.5px] py-px pl-[6px] pr-[9px] text-caption font-bold ${tone}`}>
      <Icon name={critical ? 'error' : 'warning'} size={15} />
      {severityLabel(severity)}
    </span>
  )
}

const chipTone: Record<string, { box: string; icon: IconName }> = {
  normal: { box: 'border-normal bg-normal-bg text-normal border-[1.5px]', icon: 'schedule' },
  warning: { box: 'border-warning bg-warning-bg text-warning border-[3px]', icon: 'warning' },
  unknown: { box: 'border-unknown bg-unknown-bg text-unknown border-[1.5px]', icon: 'help' },
  disrupted: { box: 'border-disrupted bg-disrupted text-on-disrupted border-0', icon: 'cancel' },
}

/** The four status groups of the design system, each with its own icon and outline weight (D-47, D-51). */
export function StatusChip({ group, children, icon }: { group: string; children: ReactNode; icon?: IconName }) {
  const tone = chipTone[group.toLowerCase()] ?? chipTone.unknown!
  return (
    <span className={`inline-flex items-center gap-xs whitespace-nowrap rounded-pill px-[10px] py-[2px] text-label font-bold ${tone.box}`}>
      <Icon name={icon ?? tone.icon} size={16} />
      {children}
    </span>
  )
}

export function Th({ children }: { children: ReactNode }) {
  return (
    <th scope="col" className="border-b border-line px-[10px] py-[6px] text-left text-caption font-bold text-muted">
      {children}
    </th>
  )
}

export const cell = 'border-b border-line px-[10px] py-[7px] align-middle text-label tabular-nums'
