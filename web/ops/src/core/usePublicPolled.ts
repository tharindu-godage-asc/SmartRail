import { useQuery } from '@tanstack/react-query'
import { ApiError, reasonOf } from './api/client'
import { config } from './config'

/** A read that needs no sign-in: station displays have no user (FR-05). Never sends a token. */
export async function getPublic<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${config.apiBaseUrl}${path}`, { signal })
  if (!response.ok) {
    throw new ApiError(response.status, await reasonOf(response))
  }

  return (await response.json()) as T
}

/**
 * Polls a public endpoint. TanStack Query keeps the last good answer when a refresh fails, so a display never goes blank on an error.
 * Unlike the controller screens it keeps polling while its tab is in the background, because a display is a tab nobody looks away from.
 */
export function usePublicPolled<T>(key: string, path: string, intervalMs: number = config.displayPollMs, enabled = true) {
  return useQuery({
    queryKey: [key, path],
    enabled,
    queryFn: ({ signal }) => getPublic<T>(path, signal),
    refetchInterval: intervalMs,
    refetchIntervalInBackground: true,
    retry: false,
    networkMode: 'always',
  })
}
