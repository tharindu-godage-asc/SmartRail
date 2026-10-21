import type { AssignmentJourney } from '../../core/api/types'
import type { IconName } from '../../core/Icon'

/** Where a journey is in its day, from its stored status: not started (no operator yet, or assigned but no report), ongoing, completed or cancelled. */
export type Stage = 'notStarted' | 'ongoing' | 'completed' | 'cancelled'

export const stageOf = (status: string): Stage =>
  status === 'Running' ? 'ongoing' : status === 'Completed' ? 'completed' : status === 'Cancelled' ? 'cancelled' : 'notStarted'

export const stageIcon: Record<Stage, IconName> = { notStarted: 'schedule', ongoing: 'train', completed: 'done', cancelled: 'cancel' }
export const stages: Stage[] = ['notStarted', 'ongoing', 'completed', 'cancelled']

/** Applies the status and the train number filters. A journey that nobody covers is never "ongoing": a report needs an assignment. */
export const filterJourneys = (rows: AssignmentJourney[], stage: Stage | 'all', text: string): AssignmentJourney[] =>
  rows.filter((r) => (stage === 'all' || stageOf(r.status) === stage) && (text === '' || r.trainNumber.toLowerCase().includes(text.toLowerCase())))
