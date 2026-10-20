import { useParams } from 'react-router'
import { Icon } from '../../core/Icon'
import { formatClock, formatClockShort, formatDate, formatDayAndTime } from '../../core/time'
import { d, type Tri } from './displayStrings'
import { DisplayFooter, Message, OfflineBanner, Stage, StatusChip, TriText } from './parts'
import { expectedTime, isTomorrow, platformScreen, statusOf, statusText, upcomingOf, type PlatformScreen } from './screens'
import { UpcomingTrains } from './UpcomingTrains'
import type { Board, BoardCall } from './types'
import { boardPath, useBoard, validPlatform, validStation } from './useBoard'

/** One train on one platform, with "when is my train arriving?" as the largest thing on the screen (FR-81, D-50, spec 09). */
const UPCOMING_COUNT = 6

export function PlatformDisplay() {
  const { stationCode, platform } = useParams()
  const valid = validStation(stationCode) && validPlatform(platform)
  // The first train and up to five more, shown below it as the next trains (spec 09 section 16).
  const state = useBoard(valid ? boardPath(stationCode, platform, UPCOMING_COUNT) : null)

  if (state.notSetUp) return <Message text={d.notSetUp} />
  if (!state.board) return <Message text={state.neverLoaded ? d.liveUnavailable : d.connecting} />

  const board = state.board
  const screen = platformScreen(board)
  return (
    <Stage>
      {state.offline ? <OfflineBanner lastUpdate={board.serverTime} /> : null}
      <header className="flex items-center justify-between px-[72px] pt-[40px]">
        <div className="flex items-baseline gap-[28px]">
          <div className="text-level2 font-bold">
            {d.platform.en} {platform}
          </div>
          <StationNames board={board} />
        </div>
        <div className="flex items-baseline gap-[40px] text-level3 font-semibold tabular-nums">
          <span>{formatDate(state.serverNow ?? board.serverTime)}</span>
          <span>{formatClock(state.serverNow ?? board.serverTime)}</span>
        </div>
      </header>
      <main className="flex grow flex-col gap-[16px] px-[72px] pb-[28px] pt-[16px]">
        {/* Keyed by the lead train: when it leaves, the new first train and the list rise into place. */}
        <div
          key={screen.kind === 'train' ? `${screen.call.trainNumber}-${screen.call.operatingDate}` : screen.kind}
          className="animate-rise flex grow flex-col justify-center gap-[20px]"
        >
          <Content screen={screen} board={board} offline={state.offline} />
        </div>
        {screen.kind === 'train' ? <UpcomingTrains calls={upcomingOf(board)} offline={state.offline} /> : null}
      </main>
      <DisplayFooter updatedAt={board.serverTime} offline={state.offline} />
    </Stage>
  )
}

export function StationNames({ board }: { board: Board }) {
  const { name } = board.station
  return (
    <div className="flex gap-[14px] text-level4 text-muted">
      <span lang="si" style={{ fontFamily: 'var(--font-family-sinhala)' }}>{name.si}</span>
      <span aria-hidden="true">·</span>
      <span lang="ta" style={{ fontFamily: 'var(--font-family-tamil)' }}>{name.ta}</span>
      <span aria-hidden="true">·</span>
      <span lang="en">{name.en}</span>
    </div>
  )
}

function Content({ screen, board, offline }: { screen: PlatformScreen; board: Board; offline: boolean }) {
  switch (screen.kind) {
    case 'train':
      return <TrainContent screen={screen} offline={offline} />
    case 'noTrain': {
      const time = expectedTime(screen.next)
      return (
        <>
          <TriText text={d.noTrainsUntil(time ? formatClockShort(time.at) : '')} className="text-level1-status font-bold" />
          <NextService call={screen.next} />
        </>
      )
    }
    default:
      return (
        <>
          <TriText text={d.noMoreToday} className="text-level1-status font-bold" />
          {screen.firstTomorrow ? <FirstService call={screen.firstTomorrow} serverTime={board.serverTime} /> : null}
        </>
      )
  }
}

/**
 * Says whether the time is an arrival or a departure: an arrow into or out of the station, and the word in three languages. The icon is
 * decorative, the word carries the meaning (status is never by icon alone).
 */
export function DirectionMark({ kind, label, size = 52 }: { kind: 'arrival' | 'departure'; label?: Tri; size?: number }) {
  const words = label ?? (kind === 'arrival' ? d.arrival : d.departure)
  return (
    <div className="flex items-center gap-[20px] text-muted" data-direction={kind}>
      <Icon name={kind} size={size} />
      <TriText text={words} className="text-level3 font-semibold" />
    </div>
  )
}

function Destination({ call }: { call: BoardCall }) {
  return (
    <div className="flex flex-col gap-[6px]">
      <div className="text-level3 font-semibold tabular-nums text-muted">
        {d.train.en} {call.trainNumber}
      </div>
      <TriText
        text={call.destination.name}
        order={['si', 'ta', 'en']}
        className="gap-x-[40px] font-bold"
        sizes={{ si: 'text-level3', ta: 'text-level3', en: 'text-level2' }}
      />
    </div>
  )
}

function TrainContent({ screen, offline }: { screen: Extract<PlatformScreen, { kind: 'train' }>; offline: boolean }) {
  const { call } = screen
  const time = expectedTime(call)
  const status = statusOf(call)

  if (screen.mode === 'unknown') {
    // D-39, spec 09 rule 4: no ETA, "Waiting for update" is a status, and the last position we have is shown.
    return (
      <>
        <Destination call={call} />
        <div>
          <StatusChip group="unknown" text={d.waiting.en} size="large" />
        </div>
        <TriText text={d.noNews} className="text-level3 text-muted" />
        {call.lastSeen ? (
          <div className="text-level3 text-muted">
            {d.lastSeen.en}: {call.lastSeen.state === 'Arrived' ? d.arrivedAt.en : d.departedFrom.en} {call.lastSeen.stationName}{' '}
            <span className="tabular-nums">{formatClockShort(call.lastSeen.at)}</span>
          </div>
        ) : null}
      </>
    )
  }

  if (screen.mode === 'atPlatform') {
    // The "Arrived" screen: it stays while the train is at the platform and is replaced when it leaves (the board stops listing a train
    // once it has departed). The big time is when it arrived (the report), otherwise when it was expected.
    const departs = call.departure ? (call.departure.estimated ?? call.departure.scheduled) : null
    const arrivedAt = call.lastSeen?.state === 'Arrived' ? call.lastSeen.at : (time?.at ?? null)
    return (
      <>
        <Destination call={call} />
        <div className="flex items-center gap-[56px]">
          <div className="flex flex-col gap-[10px]">
            <DirectionMark kind="arrival" label={d.arrivedAt} />
            <div className="text-level1-time font-bold leading-none tabular-nums">{arrivedAt ? formatClockShort(arrivedAt) : '—'}</div>
          </div>
          <StatusChip group="normal" text={d.atPlatform.en} icon="done" />
        </div>
        <TriText text={d.atPlatform} className="text-level3 text-muted" order={['si', 'ta']} />
        {departs ? (
          <div className="flex items-center gap-[20px] text-level3 tabular-nums">
            <Icon name="departure" size={44} />
            <span>{d.departs.en} {formatClockShort(departs)}</span>
          </div>
        ) : null}
      </>
    )
  }

  const label: Tri | null = !time
    ? null
    : offline
      ? time.kind === 'arrival' ? d.lastKnownArrival : d.lastKnownDeparture
      : null

  return (
    <>
      <Destination call={call} />
      <div className="flex items-center gap-[56px]">
        <div className="flex flex-col gap-[10px]">
          {time ? <DirectionMark kind={time.kind} label={label ?? undefined} /> : null}
          <div className={`text-level1-time font-bold leading-none tabular-nums ${offline ? 'text-muted' : ''}`}>
            {time ? formatClockShort(time.at) : '—'}
          </div>
        </div>
        <StatusChip group={status.group} text={statusText(status.kind, call.delayMinutes)} />
      </div>
    </>
  )
}

export function NextService({ call }: { call: BoardCall }) {
  const time = expectedTime(call)
  return (
    <div className="flex flex-col gap-[8px] text-level3 text-muted">
      <TriText text={d.nextService} />
      <div className="flex flex-wrap items-baseline gap-x-[28px] tabular-nums text-ink">
        <span className="font-bold">{time ? formatClockShort(time.at) : ''}</span>
        <span>{d.train.en} {call.trainNumber}</span>
        <TriText text={call.destination.name} order={['si', 'ta', 'en']} />
      </div>
    </div>
  )
}

export function FirstService({ call, serverTime }: { call: BoardCall; serverTime: string }) {
  const time = expectedTime(call)
  if (!time) return null
  const tomorrow = isTomorrow(time.at, serverTime)
  return (
    <div className="flex flex-col gap-[8px] text-level3 text-muted">
      {tomorrow ? (
        <TriText text={d.firstTomorrow(formatClockShort(time.at))} />
      ) : (
        <>
          <TriText text={d.firstService} />
          <span className="tabular-nums text-ink">{formatDayAndTime(time.at)}</span>
        </>
      )}
      <div className="flex flex-wrap items-baseline gap-x-[28px] tabular-nums text-ink">
        <span>{d.train.en} {call.trainNumber}</span>
        <TriText text={call.destination.name} order={['si', 'ta', 'en']} />
      </div>
    </div>
  )
}
