/**
 * The roles in an access token's flat `roles` claim (spec 02). The realm puts them in the access token only, so the app reads the
 * token's payload. This only chooses which screen to show: the API checks the signature and the role on every call (FR-02).
 */
export function rolesOf(accessToken: string | undefined): string[] {
  if (!accessToken) {
    return []
  }

  try {
    const payload = accessToken.split('.')[1]
    if (!payload) {
      return []
    }

    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '=')
    const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0))
    const claims: unknown = JSON.parse(new TextDecoder().decode(bytes))
    if (typeof claims === 'object' && claims !== null && 'roles' in claims && Array.isArray(claims.roles)) {
      return claims.roles.filter((role): role is string => typeof role === 'string')
    }
  } catch {
    // Not a readable token: no roles.
  }

  return []
}

export const isController = (accessToken: string | undefined): boolean => rolesOf(accessToken).includes('controller')
