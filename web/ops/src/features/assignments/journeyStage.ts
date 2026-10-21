import type { AssignmentJourney } from '../../core/api/types'
import type { IconName } from '../../core/Icon'

/**
 * Where a journey is in its day: overdue to start (nothing has started it although its departure time has passed), not started yet,
 * ongoing, completed or cancelled. The server decides "overdue" (it knows the clock and the grace period).
 */
export type Stage = 'overdue' | 'notStarted' | 'ongoing' | 'completed' | 'cancelled'

export const stageOf = (journey: Pick<AssignmentJourney, 'status' | 'overdueToStart'>): Stage =>
  journey.status === 'Running'
    ? 'ongoing'
    : journey.status === 'Completed'
      ? 'completed'
      : journey.status === 'Cancelled'
        ? 'cancelled'
        : journey.overdueToStart
          ? 'overdue'
          : 'notStarted'

export const stageIcon: Record<Stage, IconName> = { overdue: 'warning', notStarted: 'schedule', ongoing: 'train', completed: 'done', cancelled: 'cancel' }
export const stages: Stage[] = ['overdue', 'notStarted', 'ongoing', 'completed', 'cancelled']

/** Applies the status and the train number filters. */
export const filterJourneys = (rows: AssignmentJourney[], stage: Stage | 'all', text: string): AssignmentJourney[] =>
  rows.filter((r) => (stage === 'all' || stageOf(r) === stage) && (text === '' || r.trainNumber.toLowerCase().includes(text.toLowerCase())))
