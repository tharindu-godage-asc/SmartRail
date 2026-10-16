import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { ResolveAction } from '../../core/api/types'
import { useApi } from '../auth/apiContext'

/** Resolves one exception. Win or lose (409, 404), both panels are refreshed straight away so the screen shows the truth. */
export function useResolve() {
  const { api } = useApi()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: ResolveAction }) => api.post(`/api/v1/exceptions/${id}/resolve`, { action }),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['exceptions'] })
      void queryClient.invalidateQueries({ queryKey: ['closedExceptions'] })
      void queryClient.invalidateQueries({ queryKey: ['overview'] })
    },
  })
}
