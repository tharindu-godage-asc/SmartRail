import { useMemo, useState, type ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'
import { createApi, type Blocked } from '../../core/api/client'
import { config } from '../../core/config'
import { ApiContext } from './apiContext'

/** One API client for the signed-in user. It renews the token once on a 401 and says when the session cannot go on. */
export function ApiProvider({ children }: { children: ReactNode }) {
  const { user, signinSilent } = useAuth()
  const token = user?.access_token
  const [blocked, setBlocked] = useState<Blocked>(null)

  const value = useMemo(
    () => ({
      blocked,
      api: createApi({
        baseUrl: config.apiBaseUrl,
        getToken: () => token,
        renew: async () => {
          try {
            return (await signinSilent())?.access_token
          } catch {
            return undefined
          }
        },
        onBlocked: setBlocked,
      }),
    }),
    [token, signinSilent, blocked],
  )

  return <ApiContext.Provider value={value}>{children}</ApiContext.Provider>
}
