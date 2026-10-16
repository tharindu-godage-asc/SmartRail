import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { makeToken } from '../../test/helpers'
import { AuthGate } from './AuthGate'
import { useApi } from './apiContext'

interface FakeAuth {
  isLoading: boolean
  isAuthenticated: boolean
  error?: Error
  user?: { access_token: string; profile: Record<string, string> }
  signinRedirect: ReturnType<typeof vi.fn>
  signoutRedirect: ReturnType<typeof vi.fn>
  signinSilent: ReturnType<typeof vi.fn>
}

let auth: FakeAuth
vi.mock('react-oidc-context', () => ({ useAuth: () => auth }))

const signedIn = (roles: string[]): FakeAuth => ({
  isLoading: false,
  isAuthenticated: true,
  user: { access_token: makeToken({ roles }), profile: { preferred_username: roles.includes('controller') ? 'jayasinghe' : 'nimal' } },
  signinRedirect: vi.fn(),
  signoutRedirect: vi.fn(),
  signinSilent: vi.fn(),
})

function Dashboard() {
  useApi() // proves the API client exists only inside the gate
  return <p>The dashboard</p>
}

beforeEach(() => {
  auth = { isLoading: false, isAuthenticated: false, signinRedirect: vi.fn(), signoutRedirect: vi.fn(), signinSilent: vi.fn() }
})

describe('AuthGate (AC-E01-02)', () => {
  it('shows the sign-in screen with the safety notice when nobody is signed in, and the button starts the sign-in', async () => {
    render(<AuthGate><Dashboard /></AuthGate>)

    expect(screen.getByText(/information system, not a safety system/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sign in with staff account' }))

    expect(auth.signinRedirect).toHaveBeenCalledTimes(1)
    expect(screen.queryByText('The dashboard')).not.toBeInTheDocument()
  })

  it('shows a failure message when the sign-in came back with an error', () => {
    auth.error = new Error('login_required')

    render(<AuthGate><Dashboard /></AuthGate>)

    expect(screen.getByRole('alert')).toHaveTextContent('Could not sign you in')
  })

  it('shows the dashboard to a controller', () => {
    auth = signedIn(['controller'])

    render(<AuthGate><Dashboard /></AuthGate>)

    expect(screen.getByText('The dashboard')).toBeInTheDocument()
  })

  it('shows "No dashboard access" to an operator, names the account, and never renders the dashboard (so no data request is made)', async () => {
    auth = signedIn(['operator'])

    render(<AuthGate><Dashboard /></AuthGate>)

    expect(screen.getByRole('heading', { name: 'This account cannot open the dashboard' })).toBeInTheDocument()
    expect(screen.getByText('nimal')).toBeInTheDocument()
    expect(screen.queryByText('The dashboard')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(auth.signoutRedirect).toHaveBeenCalledTimes(1)
  })

  it('treats a token with no roles claim like an operator', () => {
    auth = signedIn([])

    render(<AuthGate><Dashboard /></AuthGate>)

    expect(screen.getByRole('heading', { name: 'This account cannot open the dashboard' })).toBeInTheDocument()
  })

  it('waits while the sign-in is being completed', () => {
    auth.isLoading = true

    render(<AuthGate><Dashboard /></AuthGate>)

    expect(screen.getByRole('status')).toHaveTextContent('Signing in…')
  })
})
