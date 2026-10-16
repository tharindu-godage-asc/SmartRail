/** An error answer from the API: the status and the stable `reason` code of the ProblemDetails body. */
export class ApiError extends Error {
  readonly status: number
  readonly reason: string | null

  constructor(status: number, reason: string | null) {
    super(`The API answered ${status}${reason ? ` (${reason})` : ''}`)
    this.name = 'ApiError'
    this.status = status
    this.reason = reason
  }
}

export type Blocked = 'session-ended' | 'forbidden' | null

export interface ApiOptions {
  baseUrl: string
  /** The current access token. It is sent only to `baseUrl`, and never logged or put in a URL. */
  getToken: () => string | undefined
  /** Try to get a new token quietly. Returns it, or undefined when that is not possible. Called once per 401. */
  renew: () => Promise<string | undefined>
  onBlocked: (reason: Exclude<Blocked, null>) => void
  fetchFn?: typeof fetch
}

export interface Api {
  get: <T>(path: string, signal?: AbortSignal) => Promise<T>
  post: (path: string, body: unknown, signal?: AbortSignal) => Promise<void>
}

export function createApi(options: ApiOptions): Api {
  const doFetch = options.fetchFn ?? fetch

  async function send(method: string, path: string, body: unknown, signal?: AbortSignal): Promise<Response> {
    const call = async (token: string | undefined) => {
      if (!token) {
        return new Response(null, { status: 401 })
      }

      return doFetch(`${options.baseUrl}${path}`, {
        method,
        signal,
        headers: {
          Authorization: `Bearer ${token}`,
          ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
      })
    }

    let response = await call(options.getToken())
    if (response.status === 401) {
      const renewed = await options.renew()
      if (renewed) {
        response = await call(renewed)
      }
    }

    if (response.status === 401) {
      options.onBlocked('session-ended')
    } else if (response.status === 403) {
      options.onBlocked('forbidden')
    }

    if (!response.ok) {
      throw new ApiError(response.status, await reasonOf(response))
    }

    return response
  }

  return {
    get: async <T>(path: string, signal?: AbortSignal) => (await send('GET', path, undefined, signal)).json() as Promise<T>,
    post: async (path, body, signal) => {
      await send('POST', path, body ?? {}, signal)
    },
  }
}

export async function reasonOf(response: Response): Promise<string | null> {
  try {
    const body: unknown = await response.json()
    if (typeof body === 'object' && body !== null && 'reason' in body && typeof body.reason === 'string') {
      return body.reason
    }
  } catch {
    // No JSON body (a proxy error, a 204): there is no reason code.
  }

  return null
}
