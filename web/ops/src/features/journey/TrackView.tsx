import type { ControllerJourneyDetail } from '../../core/api/types'
import { Icon } from '../../core/Icon'
import { t } from '../../core/strings'

const j = t.journeyDetail

/** Where along the stops the train is, as a number from 0 (first stop) to the last stop's index: at a stop, or half way to the next. */
function trackPosition(d: ControllerJourneyDetail): number {
  const p = d.position
  if (!p) return 0
  const i = Math.max(0, d.stops.findIndex((s) => s.stationCode === p.stationCode))
  return p.state === 'Arrived' ? i : Math.min(i + 0.5, d.stops.length - 1)
}

/**
 * A single line with the stops placed along it and this train's marker at its last known position (spec 16 F1). Built from separate
 * placed elements, not one picture. It shows places only, never a prediction of where trains will meet (SmartRail is an information
 * system, FR-63); the sentence under it gives the same information in words.
 */
export function TrackView({ detail }: { detail: ControllerJourneyDetail }) {
  const n = detail.stops.length
  const at = trackPosition(detail)
  const percent = n > 1 ? (at / (n - 1)) * 100 : 0
  const done = detail.status === 'Completed'
  return (
    <div className="px-[24px] pb-[4px] pt-[8px]" role="img" aria-label={`${j.trackLabel(detail.trainNumber)}`}>
      <div className="relative h-[64px]">
        <div className="absolute left-0 right-0 top-[44px] border-t-[3px] border-dashed border-line" aria-hidden="true" />
        <div className="absolute left-0 top-[44px] border-t-[3px] border-ink" style={{ width: `${percent}%` }} aria-hidden="true" />
        <div className="absolute top-[4px] -translate-x-1/2" style={{ left: `${percent}%` }}>
          <span className="inline-flex items-center gap-xs whitespace-nowrap rounded-md border-[2px] border-warning bg-page px-[8px] py-[3px] text-label font-bold">
            <Icon name="train" size={18} />
            {detail.trainNumber}
            <span aria-hidden="true">{done ? '' : '→'}</span>
          </span>
          <div className="mx-auto h-[14px] w-[2px] bg-ink" aria-hidden="true" />
        </div>
        {detail.stops.map((stop, i) => (
          <span
            key={stop.serviceStopId}
            aria-hidden="true"
            className={`absolute top-[37px] size-[16px] -translate-x-1/2 rounded-full border-[3px] border-ink ${stop.state === 'Passed' ? 'bg-ink' : 'bg-page'}`}
            style={{ left: `${n > 1 ? (i / (n - 1)) * 100 : 0}%` }}
          />
        ))}
      </div>
      <div className="relative h-[56px]" aria-hidden="true">
        {detail.stops.map((stop, i) => (
          <div
            key={stop.serviceStopId}
            className="absolute top-0 flex -translate-x-1/2 flex-col items-center text-center"
            style={{ left: `${n > 1 ? (i / (n - 1)) * 100 : 0}%` }}
          >
            <span className="text-label font-bold">{stop.stationName}</span>
            <span className="text-caption font-semibold">{j.states[stop.state] ?? stop.state}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
