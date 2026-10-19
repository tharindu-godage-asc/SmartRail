import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { BulkVerifyResult, VerifyAction } from '../../core/api/types'
import { useApi } from '../auth/apiContext'

/** Verifies, changes or rejects the delay reason of one exception (spec 16 F2), or verifies several. Both lists are refreshed win or lose. */
export function useVerify() {
  const { api } = useApi()
  const queryClient = useQueryClient()
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['exceptions'] })
    void queryClient.invalidateQueries({ queryKey: ['closedExceptions'] })
    void queryClient.invalidateQueries({ queryKey: ['overview'] })
  }

  const one = useMutation({
    mutationFn: ({ id, action, reasonCode }: { id: string; action: VerifyAction; reasonCode?: string }) =>
      api.post(`/api/v1/exceptions/${id}/verify`, { action, reasonCode }),
    onSettled: refresh,
  })

  const many = useMutation({
    mutationFn: (ids: string[]) => api.post('/api/v1/exceptions/verify', { ids }) as Promise<BulkVerifyResult>,
    onSettled: refresh,
  })

  return { one, many }
}
