import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../core/api/client'
import { exception, queue, renderWithApi } from '../../test/helpers'
import { OpenExceptionsTab } from './OpenExceptionsTab'

const weather = exception({ id: 'e1', trainNumber: '8058', reportedReason: 'Weather', canVerify: true })
const unknown = exception({ id: 'e2', trainNumber: '8052', severity: 'High', reportedReason: 'Unknown', canVerify: false })

function fakeApi(items = [weather, unknown], post: Api['post'] = vi.fn(async () => {})) {
  return { get: vi.fn(async () => queue(items)) as unknown as Api['get'], post } satisfies Api
}

describe('Verify a delay reason (spec 16 F2, FR-52)', () => {
  it('shows the reported reason and what passengers will see, then verifies it', async () => {
    const post = vi.fn(async () => {})
    renderWithApi(<OpenExceptionsTab />, fakeApi([weather], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8058/ }))

    expect(screen.getByText('Reported by the operator: Weather')).toBeInTheDocument()
    expect(screen.getByText(/Verified by Operations/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Verify and publish reason' }))

    expect(post).toHaveBeenCalledWith('/api/v1/exceptions/e1/verify', { action: 'verify', reasonCode: undefined })
  })

  it('locks an unapproved reason but lets the controller change it to an approved one', async () => {
    const post = vi.fn(async () => {})
    renderWithApi(<OpenExceptionsTab />, fakeApi([unknown], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8052/ }))

    expect(screen.getByText(/cannot be published as reported/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Verify and publish reason' })).toBeDisabled()

    await userEvent.click(screen.getByRole('button', { name: 'Edit reason' }))
    await userEvent.selectOptions(screen.getByLabelText('Approved reason'), 'SignalIssue')
    await userEvent.click(screen.getByRole('button', { name: 'Publish this reason' }))

    expect(post).toHaveBeenCalledWith('/api/v1/exceptions/e2/verify', { action: 'change', reasonCode: 'SignalIssue' })
  })

  it('reject sends reject and says nothing is published', async () => {
    const post = vi.fn(async () => {})
    renderWithApi(<OpenExceptionsTab />, fakeApi([weather], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8058/ }))

    expect(screen.getByText(/publishes no reason/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Reject report' }))

    expect(post).toHaveBeenCalledWith('/api/v1/exceptions/e1/verify', { action: 'reject', reasonCode: undefined })
  })

  it('explains a conflict with another controller', async () => {
    const post = vi.fn(async () => { throw new ApiError(409, 'already_resolved') })
    renderWithApi(<OpenExceptionsTab />, fakeApi([weather], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8058/ }))

    await userEvent.click(screen.getByRole('button', { name: 'Verify and publish reason' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Another controller already dealt with this item')
  })

  it('verifies several at once; a locked row cannot be ticked', async () => {
    const post = vi.fn(async () => ({ results: [{ id: 'e1', status: 'verified' }] }))
    renderWithApi(<OpenExceptionsTab />, fakeApi([weather, unknown], post))
    await screen.findAllByRole('row')

    await userEvent.click(screen.getByRole('button', { name: 'Select several to verify' }))
    const rows = screen.getAllByRole('row')
    expect(within(rows[2]!).queryByRole('checkbox')).not.toBeInTheDocument()
    expect(within(rows[2]!).getByText('Locked: change the reason first')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: 'Select train 8058 for verification' }))
    await userEvent.click(screen.getByRole('button', { name: 'Verify selected (1)' }))

    expect(post).toHaveBeenCalledWith('/api/v1/exceptions/verify', { ids: ['e1'] })
    expect(await screen.findByRole('status')).toHaveTextContent('1 verified, 0 skipped.')
  })
})
