import { createContext, useContext } from 'react'
import type { Api, Blocked } from '../../core/api/client'

export interface ApiContextValue {
  api: Api
  /** Set when the API refused the token for good: the session ended (401) or the role is gone (403). */
  blocked: Blocked
}

export const ApiContext = createContext<ApiContextValue | null>(null)

export function useApi(): ApiContextValue {
  const value = useContext(ApiContext)
  if (!value) {
    throw new Error('useApi must be used inside ApiProvider')
  }

  return value
}
