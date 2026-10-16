import { useQuery } from '@tanstack/react-query'
import { useApi } from '../features/auth/apiContext'
import { config } from './config'

/**
 * One endpoint, read every 10 s while the tab is visible (spec 08 rule 7). TanStack Query keeps the last good answer when a refresh
 * fails, never runs the same request twice at once, pauses while the tab is hidden and refreshes when it becomes visible again.
 */
export function usePolled<T>(key: string, path: string) {
  const { api } = useApi()
  return useQuery({
    queryKey: [key],
    queryFn: ({ signal }) => api.get<T>(path, signal),
    refetchInterval: config.pollMs,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    retry: false,
    // Keep trying when the browser thinks it is offline: the banner, not a silent pause, tells the controller.
    networkMode: 'always',
  })
}
