/** The SmartRail name and its two-rail mark (the design canvas). No dependencies, so the display-only build can use it alone. */
export function Wordmark({ large = false }: { large?: boolean }) {
  return (
    <span className="inline-flex flex-col items-start gap-[3px] text-ink">
      <span className={`${large ? 'text-[24px]' : 'text-[20px]'} font-bold leading-none tracking-tight`}>SmartRail</span>
      <svg width={large ? 94 : 78} height={large ? 10 : 8} viewBox="0 0 78 8" aria-hidden="true">
        <path d="M0 1.5H68M0 6.5H68" stroke="currentColor" strokeWidth="1.6" fill="none" />
        <circle cx="74" cy="4" r="3.2" fill="currentColor" />
      </svg>
    </span>
  )
}
