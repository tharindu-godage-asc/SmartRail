import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { CancellationView, CreateResult, LineOptions, ServiceDetail, ServiceRequest, ServiceRow, ValidationResult } from '../../core/api/types'
import { useApi } from '../auth/apiContext'

/**
 * The timetable reads (spec 15 rule 14): fetched when a screen opens, after a change and when the window is focused again. They are not
 * polled, because the timetable only changes when a controller changes it, and they are not part of the shell's freshness banner.
 */
const base = '/api/v1/timetable'
const line = 'line=MAIN'

export function useTimetableOptions() {
  const { api } = useApi()
  return useQuery({
    queryKey: ['timetable', 'options'],
    queryFn: ({ signal }) => api.get<LineOptions>(`${base}/options?${line}`, signal),
    staleTime: Infinity,
    retry: false,
  })
}

export function useWeekly() {
  const { api } = useApi()
  return useQuery({
    queryKey: ['timetable', 'weekly'],
    queryFn: ({ signal }) => api.get<ServiceRow[]>(`${base}/services?${line}&tab=weekly`, signal),
    retry: false,
  })
}

export function useExtra() {
  const { api } = useApi()
  return useQuery({
    queryKey: ['timetable', 'extra'],
    queryFn: ({ signal }) => api.get<ServiceRow[]>(`${base}/services?${line}&tab=extra`, signal),
    retry: false,
  })
}

export function useCancelled() {
  const { api } = useApi()
  return useQuery({
    queryKey: ['timetable', 'cancelled'],
    queryFn: ({ signal }) => api.get<CancellationView[]>(`${base}/services?${line}&tab=cancelled`, signal),
    retry: false,
  })
}

export function useServiceDetail(serviceId: string) {
  const { api } = useApi()
  return useQuery({
    queryKey: ['timetable', 'service', serviceId],
    queryFn: ({ signal }) => api.get<ServiceDetail>(`${base}/services/${serviceId}`, signal),
    retry: false,
  })
}

/** Asks the server whether a draft is valid. Saves nothing; the answer is always 200 (spec 13 section 4.4). */
export function useValidate() {
  const { api } = useApi()
  return useMutation({
    mutationFn: (request: ServiceRequest) => api.post(`${base}/validate`, request) as Promise<ValidationResult>,
  })
}

/** Schedules a service. Win or lose, the timetable reads are refreshed so the screens show the truth. */
export function useCreateService() {
  const { api } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: ServiceRequest) => api.post(`${base}/services`, request) as Promise<CreateResult>,
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['timetable'] }),
  })
}
