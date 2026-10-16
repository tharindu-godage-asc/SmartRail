import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../core/api/client'
import { exception, queue, renderWithApi } from '../../test/helpers'
import { OpenExceptionsTab } from './OpenExceptionsTab'

const critical = exception({ id: 'e1', trainNumber: '8058', stationName: 'Gampaha', ageSeconds: 180 })
const high = exception({ id: 'e2', trainNumber: '8052', stationName: null, type: 'EarlyDeparture', severity: 'High', ageSeconds: 600 })

function fakeApi(items = [critical, high], post: Api['post'] = vi.fn(async () => {})) {
  return { get: vi.fn(async () => queue(items)) as unknown as Api['get'], post } satisfies Api
}

describe('Open exceptions tab', () => {
  it('lists the open exceptions in the API order with severity as icon and word, where, and age (AC-E05-02 3, 4)', async () => {
    renderWithApi(<OpenExceptionsTab />, fakeApi())

    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + two
    const first = within(rows[1]!)
    expect(first.getByText('Critical')).toBeInTheDocument()
    expect(first.getByText('Delay reason waiting for verification')).toBeInTheDocument()
    expect(first.getByText('8058 · Gampaha')).toBeInTheDocument()
    expect(first.getByText(/·\s*3 min/)).toBeInTheDocument()

    const second = within(rows[2]!)
    expect(second.getByText('High')).toBeInTheDocument()
    expect(second.getByText('Departed before scheduled time')).toBeInTheDocument()
    expect(second.getByText('8052')).toBeInTheDocument() // no station: the train only
  })

  it('shows a neutral empty state', async () => {
    renderWithApi(<OpenExceptionsTab />, fakeApi([]))

    expect(await screen.findByText('No open exceptions')).toBeInTheDocument()
  })

  it('shows an unknown exception type by its code', async () => {
    renderWithApi(<OpenExceptionsTab />, fakeApi([exception({ type: 'PlatformConflict' })]))

    expect(await screen.findByRole('button', { name: /PlatformConflict/ })).toBeInTheDocument()
  })

  it('selecting a row shows the detail and the three actions', async () => {
    renderWithApi(<OpenExceptionsTab />, fakeApi())
    await userEvent.click(await screen.findByRole('button', { name: /Delay reason waiting for verification, train 8058|Select Delay reason/ }))

    expect(screen.getByText(/reason is waiting for verification/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Acknowledge' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'No action needed' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Escalate' })).toBeInTheDocument()
  })

  it('resolves only after the confirm, and a double click sends one request (AC-E05-02 6, rule 9)', async () => {
    let finish: () => void = () => {}
    const post = vi.fn(() => new Promise<void>((resolve) => { finish = resolve }))
    renderWithApi(<OpenExceptionsTab />, fakeApi([critical, high], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8058/ }))

    await userEvent.click(screen.getByRole('button', { name: 'Escalate' }))
    expect(post).not.toHaveBeenCalled() // the dialog asks first
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/“Escalated”/)).toBeInTheDocument()

    const confirm = within(dialog).getByRole('button', { name: 'Confirm' })
    await userEvent.dblClick(confirm)
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith('/api/v1/exceptions/e1/resolve', { action: 'Escalated' })
    expect(within(dialog).getByRole('button', { name: 'Sending…' })).toBeDisabled()

    finish()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('Cancel sends nothing', async () => {
    const post = vi.fn(async () => {})
    renderWithApi(<OpenExceptionsTab />, fakeApi([critical], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8058/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Acknowledge' }))

    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }))

    expect(post).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('explains a 409: already resolved by another controller', async () => {
    const post = vi.fn(async () => { throw new ApiError(409, 'already_resolved') })
    renderWithApi(<OpenExceptionsTab />, fakeApi([critical], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8058/ }))
    await userEvent.click(screen.getByRole('button', { name: 'No action needed' }))

    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Already resolved by another controller.')
  })

  it('explains a 404: the exception no longer exists', async () => {
    const post = vi.fn(async () => { throw new ApiError(404, 'exception_not_found') })
    renderWithApi(<OpenExceptionsTab />, fakeApi([critical], post))
    await userEvent.click(await screen.findByRole('button', { name: /train 8058/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Acknowledge' }))

    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Confirm' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('This exception no longer exists.')
  })

  it('keeps showing the last list when a refresh fails (never cleared on an error)', async () => {
    const get = vi.fn().mockResolvedValueOnce(queue([critical])).mockRejectedValue(new ApiError(500, null))
    const api = { get: get as unknown as Api['get'], post: vi.fn() } satisfies Api
    const { client } = renderWithApi(<OpenExceptionsTab />, api)
    await screen.findByText('8058 · Gampaha')

    await client.refetchQueries({ queryKey: ['exceptions'] })

    expect(get).toHaveBeenCalledTimes(2)
    expect(screen.getByText('8058 · Gampaha')).toBeInTheDocument()
  })
})
