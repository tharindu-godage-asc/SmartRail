import type { Dispatch } from 'react'
import type { LineOptions, ValidationIssue } from '../../../core/api/types'
import { t } from '../../../core/strings'
import { cell, Th } from '../../../core/ui'
import { inRunningOrder, isLocked, type Action, type Draft } from './draft'
import { issuesForStop } from './issues'
import { IssueList } from './parts'
import { inputClass } from './styles'

/** Step 2: Every stop or Choose stops, and a time and a platform for each stop (spec 15 rules 4 to 6, `Dash-NewService-Stops`). */
export function StopsStep({
  draft,
  options,
  dispatch,
  issues,
}: {
  draft: Draft
  options: LineOptions
  dispatch: Dispatch<Action>
  issues: ValidationIssue[]
}) {
  const w = t.timetable.wizard
  const run = inRunningOrder(draft)
  const byCode = new Map(options.stations.map((s) => [s.code, s]))
  const included = run.filter((s) => s.included)

  return (
    <div className="flex flex-col gap-md">
      <fieldset className="m-0 flex flex-wrap gap-sm border-0 p-0">
        <legend className="sr-only">{w.steps[1]}</legend>
        {(['every', 'choose'] as const).map((mode) => (
          <label
            key={mode}
            className={`flex min-w-[240px] flex-1 cursor-pointer flex-col gap-px rounded-md p-[12px] ${
              draft.stopMode === mode ? 'border-[3px] border-ink' : 'border border-line'
            }`}
          >
            <span className="flex items-center gap-sm text-[15px] font-bold">
              <input type="radio" name="stop-mode" checked={draft.stopMode === mode} onChange={() => dispatch({ type: 'stopMode', mode })} />
              {mode === 'every' ? w.everyStop : w.chooseStops}
            </span>
            <span className="pl-[26px] text-caption text-muted">{mode === 'every' ? w.everyStopHint : w.chooseStopsHint}</span>
          </label>
        ))}
      </fieldset>

      <p className="m-0 text-label text-muted">{w.timeHint}</p>

      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <thead>
            <tr>
              {draft.stopMode === 'choose' ? <Th>{w.include}</Th> : null}
              <Th>{w.station}</Th>
              <Th>{w.time}</Th>
              <Th>{w.platform}</Th>
              <Th>{w.notes}</Th>
            </tr>
          </thead>
          <tbody>
            {run.map((stop) => {
              const station = byCode.get(stop.stationCode)
              const name = station?.names.en ?? stop.stationCode
              const locked = isLocked(draft, stop.stationCode)
              const rowIssues = issuesForStop(issues, draft, stop.stationCode)
              const position = included.findIndex((s) => s.stationCode === stop.stationCode)
              return (
                <tr key={stop.stationCode} data-station={stop.stationCode}>
                  {draft.stopMode === 'choose' ? (
                    <td className={cell}>
                      <input
                        type="checkbox"
                        aria-label={`${w.include} ${name}`}
                        checked={stop.included}
                        disabled={locked}
                        onChange={() => dispatch({ type: 'toggleStop', stationCode: stop.stationCode })}
                        className="size-[22px]"
                      />
                    </td>
                  ) : null}
                  <td className={`${cell} font-bold`}>
                    {name}
                    {station?.names.si ? (
                      <span lang="si" className="ml-sm font-normal text-muted">
                        {station.names.si}
                      </span>
                    ) : null}
                  </td>
                  {stop.included ? (
                    <>
                      <td className={cell}>
                        <input
                          className={`${inputClass} w-[96px] tabular-nums`}
                          value={stop.time}
                          inputMode="numeric"
                          placeholder="HH:mm"
                          maxLength={5}
                          aria-label={`${w.time} ${name}`}
                          onChange={(e) => dispatch({ type: 'time', stationCode: stop.stationCode, value: e.target.value })}
                        />
                      </td>
                      <td className={cell}>
                        <select
                          className={inputClass}
                          aria-label={`${w.platform} ${name}`}
                          value={stop.platform ?? ''}
                          onChange={(e) => dispatch({ type: 'platform', stationCode: stop.stationCode, value: e.target.value || null })}
                        >
                          <option value="">{w.platformNotSet}</option>
                          {station?.platforms.map((p) => (
                            <option key={p} value={p}>
                              {`${w.platform} ${p}`}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td className={`${cell} whitespace-normal`}>
                        {position === 0 ? w.firstStop : position === included.length - 1 ? w.lastStop : ''}
                        <IssueList issues={rowIssues} />
                      </td>
                    </>
                  ) : (
                    <>
                      <td className={`${cell} text-muted`}>—</td>
                      <td className={`${cell} text-muted`}>—</td>
                      <td className={`${cell} text-muted`}>{w.doesNotStop}</td>
                    </>
                  )}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
