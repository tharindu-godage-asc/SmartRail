import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'
import { t } from '../../core/strings'
import { ApiProvider } from './ApiProvider'
import { useApi } from './apiContext'
import { isController } from './roles'
import { ForbiddenScreen, LoadingScreen, NoAccessScreen, SessionEndedScreen, SignInScreen } from './SignedOutScreens'

/**
 * Chooses the screen: loading, sign in, no access, or the dashboard. This only picks what to show: the API checks the token and the
 * role on every call (FR-02), so a hand-edited client cannot read anything the server refuses. No data request is made for a
 * person without the controller role.
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const auth = useAuth()

  if (auth.isLoading) {
    return <LoadingScreen text={t.signingIn} />
  }

  if (!auth.isAuthenticated) {
    return <SignInScreen error={Boolean(auth.error)} onSignIn={() => void auth.signinRedirect()} />
  }

  const username = auth.user?.profile.preferred_username ?? auth.user?.profile.name ?? ''
  if (!isController(auth.user?.access_token)) {
    return <NoAccessScreen username={String(username)} onSignOut={() => void auth.signoutRedirect()} />
  }

  return (
    <ApiProvider>
      <Blockable>{children}</Blockable>
    </ApiProvider>
  )
}

/** When the API refuses the token for good, the dashboard is replaced by an explanation instead of an empty screen. */
function Blockable({ children }: { children: ReactNode }) {
  const auth = useAuth()
  const { blocked } = useApi()

  if (blocked === 'session-ended') {
    return <SessionEndedScreen onSignIn={() => void auth.signinRedirect()} />
  }

  if (blocked === 'forbidden') {
    return <ForbiddenScreen onSignOut={() => void auth.signoutRedirect()} />
  }

  return <>{children}</>
}
