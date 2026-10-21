import { useParams } from 'react-router'
import { Icon } from '../../core/Icon'
import { formatClock, formatClockShort } from '../../core/time'
import { d } from './displayStrings'
import { DisplayFooter, Message, OfflineBanner, Stage, StatusChip, TriText } from './parts'
import { FirstService, NextService } from './PlatformDisplay'
import { expectedTime, statusOf, statusText } from './screens'
import type { Board, BoardCall } from './types'
import { boardPath, useBoard, validStation } from './useBoard'

/**
 * The main board of a station (FR-80, D-18): the next trains, one row each, with time, destination in three languages, platform and
 * status. When offline it shows the scheduled times and platforms, marked "not live" (AC-E09-01 4, EC-111), not the last estimates.
 */
export function MainBoard() {
  const { stationCode } = useParams()
  const state = useBoard(validStation(stationCode) ? boardPath(stationCode) : null)

  if (state.notSetUp) return <Message text={d.notSetUp} />
  if (!state.board) return <Message text={state.neverLoaded ? d.liveUnavailable : d.connecting} />

  const board = state.board
  return (
    <Stage>
      {state.offline ? <OfflineBanner lastUpdate={board.serverTime} /> : null}
      <header className="flex items-center justify-between px-[72px] pt-[32px]">
        <div className="flex items-baseline gap-[28px]">
          <TriText text={board.station.name} order={['en', 'si', 'ta']} className="gap-x-[28px] font-bold" sizes={{ en: 'text-level2', si: 'text-level3', ta: 'text-level3' }} />
        </div>
        <div className="text-level3 font-semibold tabular-nums">{formatClock(state.serverNow ?? board.serverTime)}</div>
      </header>
      <main className="flex grow flex-col px-[72px] pb-[16px] pt-[16px]">
        {board.trains.length === 0 ? <NothingDue board={board} /> : <Rows board={board} offline={state.offline} />}
      </main>
      <DisplayFooter updatedAt={board.serverTime} offline={state.offline} />
    </Stage>
  )
}

function NothingDue({ board }: { board: Board }) {
  const next = board.next ? expectedTime(board.next) : null
  return (
    <div className="flex grow flex-col justify-center gap-[28px]">
      {board.next ? (
        <>
          <TriText text={d.noTrainsUntil(next ? formatClockShort(next.at) : '')} className="text-level1-status font-bold" />
          <NextService call={board.next} />
        </>
      ) : (
        <>
          <TriText text={d.noMoreToday} className="text-level1-status font-bold" />
          {board.firstTomorrow ? <FirstService call={board.firstTomorrow} serverTime={board.serverTime} /> : null}
        </>
      )}
    </div>
  )
}

const columns = 'grid grid-cols-[330px_1fr_170px_560px] items-center gap-x-[24px]'

/** A train with no platform yet: the controller sets it later (spec 20). "TBA" is the same abbreviation in all three languages of the board. */
const PLATFORM_TBA = 'TBA'

function Rows({ board, offline }: { board: Board; offline: boolean }) {
  return (
    <>
      <div className={`${columns} border-b border-line pb-[8px] text-level4 text-muted whitespace-nowrap`} aria-hidden="true">
        <span>{d.columns.time.en} · {d.columns.time.si} · {d.columns.time.ta}</span>
        <span>{d.columns.destination.en} · {d.columns.destination.si} · {d.columns.destination.ta}</span>
        <span>{d.columns.platform.en}</span>
        <span>{d.columns.status.en} · {d.columns.status.si} · {d.columns.status.ta}</span>
      </div>
      <ul className="m-0 flex list-none flex-col p-0">
        {board.trains.map((call) => (
          <Row key={`${call.trainNumber}-${call.operatingDate}`} call={call} offline={offline} />
        ))}
      </ul>
    </>
  )
}

function Row({ call, offline }: { call: BoardCall; offline: boolean }) {
  const time = expectedTime(call)
  const status = statusOf(call)
  // Offline: the scheduled time, not the last estimate, and the status says it is not live (EC-111).
  const shown = time ? (offline ? time.scheduled : time.at) : null

  return (
    <li className={`${columns} h-[92px] border-b border-line`}>
      <span className={`flex items-center gap-[16px] text-level2 font-bold leading-none tabular-nums ${offline ? 'text-muted' : ''}`}>
        {time ? (
          <>
            <Icon name={time.kind} size={40} />
            <span className="sr-only">{time.kind === 'arrival' ? d.arrival.en : d.departure.en}</span>
          </>
        ) : null}
        {shown ? formatClockShort(shown) : '—'}
      </span>
      <div className="flex flex-col">
        <div className="flex items-baseline gap-[20px]">
          <span className="text-level3 font-bold leading-[1.15]" lang="en">{call.destination.name.en}</span>
          <span className="text-level4 tabular-nums text-muted">{call.trainNumber}</span>
        </div>
        <div className="flex gap-[20px] text-level4 text-muted">
          <span lang="si" style={{ fontFamily: 'var(--font-family-sinhala)' }}>{call.destination.name.si}</span>
          <span lang="ta" style={{ fontFamily: 'var(--font-family-tamil)' }}>{call.destination.name.ta}</span>
        </div>
      </div>
      <span className="text-level2 font-bold tabular-nums">{call.platform ?? PLATFORM_TBA}</span>
      <div className="flex flex-col items-start gap-[2px]">
        {offline ? (
          <StatusChip group="unknown" text={d.notLive.en} size="small" />
        ) : (
          <StatusChip group={status.group} text={statusText(status.kind, call.delayMinutes)} size="small" />
        )}
      </div>
    </li>
  )
}
