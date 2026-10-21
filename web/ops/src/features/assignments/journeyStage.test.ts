import { describe, expect, it } from 'vitest'
import type { AssignmentJourney } from '../../core/api/types'
import { filterJourneys, stageOf } from './journeyStage'

const j = (trainNumber: string, status: string): AssignmentJourney => ({
  journeyId: trainNumber, trainNumber, from: 'A', to: 'B', operatingDate: '2026-10-21', firstDeparture: '2026-10-21T03:00:00Z', lastArrival: '2026-10-21T04:00:00Z', status, source: 'Weekly', operator: null,
})

describe('Journey stages for the filters', () => {
  it('maps the stored status to not started, ongoing, completed or cancelled', () => {
    expect(stageOf('Unassigned')).toBe('notStarted')
    expect(stageOf('Scheduled')).toBe('notStarted')
    expect(stageOf('Running')).toBe('ongoing')
    expect(stageOf('Completed')).toBe('completed')
    expect(stageOf('Cancelled')).toBe('cancelled')
  })

  it('filters by stage and by part of the train number, together', () => {
    const rows = [j('8104', 'Running'), j('8106', 'Scheduled'), j('8108', 'Completed'), j('8201', 'Unassigned')]

    expect(filterJourneys(rows, 'all', '').length).toBe(4)
    expect(filterJourneys(rows, 'notStarted', '').map((r) => r.trainNumber)).toEqual(['8106', '8201'])
    expect(filterJourneys(rows, 'ongoing', '').map((r) => r.trainNumber)).toEqual(['8104'])
    expect(filterJourneys(rows, 'all', '810').map((r) => r.trainNumber)).toEqual(['8104', '8106', '8108'])
    expect(filterJourneys(rows, 'completed', '8201')).toEqual([])
  })
})
