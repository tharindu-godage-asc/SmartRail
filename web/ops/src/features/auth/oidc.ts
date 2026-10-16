import { InMemoryWebStorage, WebStorageStateStore, type UserManagerSettings } from 'oidc-client-ts'
import { config } from '../../core/config'

/**
 * Authorization Code with PKCE, as a public client (no secret). Q4 of spec 08:
 * - the signed-in user, with the access token, lives in memory only (nothing in local or session storage);
 * - the short-lived login state (the PKCE verifier and the state value) must survive the redirect to Keycloak and back, so it is
 *   kept in this tab's session storage and removed by the library when the sign-in completes.
 * A reload therefore needs one click on "Sign in", which is instant while the Keycloak session is alive.
 */
export function createOidcSettings(origin: string = window.location.origin): UserManagerSettings {
  return {
    authority: config.oidcAuthority,
    client_id: config.oidcClientId,
    redirect_uri: `${origin}/`,
    post_logout_redirect_uri: `${origin}/`,
    response_type: 'code',
    scope: 'openid profile',
    userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
    stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
    automaticSilentRenew: true,
  }
}

/** After the redirect back, take the code and state out of the address bar. */
export function onSigninCallback(): void {
  window.history.replaceState({}, document.title, window.location.pathname)
}
