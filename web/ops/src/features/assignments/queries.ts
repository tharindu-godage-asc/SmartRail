import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { AssignmentJourney, AssignmentView, AssignRequest, AssignResult, EndReason, OperatorChoice } from '../../core/api/types'
import { useApi } from '../auth/apiContext'

/**
 * The assignment reads (spec 17 A2): fetched when a screen opens, after a change and on window focus. Not polled: assignments change
 * only when a controller changes them, so they are not part of the shell's freshness banner.
 */
const base = '/api/v1'

export function useAssignmentJourneys(date: string, filter: 'unassigned' | 'all') {
  const { api } = useApi()
  return useQuery({
    queryKey: ['assignments', 'journeys', date, filter],
    queryFn: ({ signal }) => api.get<AssignmentJourney[]>(`${base}/assignments/journeys?date=${date}&filter=${filter}`, signal),
    retry: false,
  })
}

export function useOperators(journeyId: string) {
  const { api } = useApi()
  return useQuery({
    queryKey: ['assignments', 'operators', journeyId],
    queryFn: ({ signal }) => api.get<OperatorChoice[]>(`${base}/assignments/operators?journeyId=${journeyId}`, signal),
    retry: false,
  })
}

export function useJourneyAssignments(journeyId: string) {
  const { api } = useApi()
  return useQuery({
    queryKey: ['assignments', 'journey', journeyId],
    queryFn: ({ signal }) => api.get<AssignmentView[]>(`${base}/journeys/${journeyId}/assignments`, signal),
    retry: false,
  })
}

/** Assigns an operator (also a crew change). Win or lose, the assignment reads are refreshed. */
export function useAssign(journeyId: string) {
  const { api } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: AssignRequest) => api.post(`${base}/journeys/${journeyId}/assignments`, request) as Promise<AssignResult>,
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['assignments'] }),
  })
}

export function useEndAssignment(journeyId: string) {
  const { api } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ assignmentId, reason, note }: { assignmentId: string; reason: EndReason; note?: string }) =>
      api.post(`${base}/journeys/${journeyId}/assignments/${assignmentId}/end`, { reason, note }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['assignments'] }),
  })
}
