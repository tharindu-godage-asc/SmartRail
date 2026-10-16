import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type Api } from '../../core/api/client'
import { closedException, closedList, exception, queue, renderWithApi } from '../../test/helpers'
import { ClosedExceptionsTab } from './ClosedExceptionsTab'
import { ExceptionsPage } from './ExceptionsPage'
import { OpenExceptionsTab } from './OpenExceptionsTab'

const apiFor = (closed: Parameters<typeof closedList>[0], total?: number): Api => ({
  get: vi.fn(async (path: string) => (path.includes('resolved') ? closedList(closed, total) : queue([exception()]))) as unknown as Api['get'],
  post: vi.fn(),
})

describe('Closed exceptions tab', () => {
  it('shows each closed exception as a card with status, severity, type, where, raised, closed, time taken and who (spec 08 section 14)', async () => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([closedException()]))

    const card = within(await screen.findByRole('article'))
    expect(card.getByText('Closed · Acknowledged')).toBeInTheDocument() // status in words (and an icon)
    expect(card.getByText('Critical')).toBeInTheDocument()
    expect(card.getByText('Delay reason waiting for verification')).toBeInTheDocument()
    expect(card.getByText(/8058 · Gampaha/)).toBeInTheDocument()
    expect(card.getByText('Raised').nextSibling).toHaveTextContent('Thu 15 Oct · 14:52') // 09:22 UTC in Sri Lanka
    expect(card.getByText('Closed').nextSibling).toHaveTextContent('Thu 15 Oct · 16:41 · 1 h 49 min after it was raised')
    expect(card.getByText('Closed by').nextSibling).toHaveTextContent('jayasinghe')
  })

  it.each([
    ['NoActionNeeded', 'Closed · No action needed'],
    ['Escalated', 'Closed · Escalated'],
  ])('words the %s action', async (resolution, text) => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([closedException({ resolution })]))

    expect(await screen.findByText(text)).toBeInTheDocument()
  })

  it('says "Controller" when no name was stored, and shows no station when there is none', async () => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([closedException({ resolvedByName: null, stationName: null })]))

    const card = within(await screen.findByRole('article'))
    expect(card.getByText('Closed by').nextSibling).toHaveTextContent('Controller')
    expect(card.getByRole('heading')).toHaveTextContent('· 8058')
    expect(card.getByRole('heading')).not.toHaveTextContent('Gampaha')
  })

  it('keeps the order the API sends (newest closure first)', async () => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([closedException({ id: 'a', trainNumber: '8001' }), closedException({ id: 'b', trainNumber: '8002' })]))

    const cards = await screen.findAllByRole('article')

    expect(cards[0]).toHaveTextContent('8001')
    expect(cards[1]).toHaveTextContent('8002')
  })

  it('says how many it shows when there are more than the page holds', async () => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([closedException()], 214))

    expect(await screen.findByText('Showing the latest 1 of 214')).toBeInTheDocument()
  })

  it('says nothing about a limit when everything is shown', async () => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([closedException()]))

    await screen.findByRole('article')
    expect(screen.queryByText(/Showing the latest/)).not.toBeInTheDocument()
  })

  it('has a neutral empty state', async () => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([]))

    expect(await screen.findByText('No closed exceptions yet')).toBeInTheDocument()
  })

  it('says it could not load when the first read fails', async () => {
    const api: Api = {
      get: vi.fn(async () => {
        throw new ApiError(500, null)
      }) as unknown as Api['get'],
      post: vi.fn(),
    }
    renderWithApi(<ClosedExceptionsTab />, api)

    expect(await screen.findByText(/Could not load this yet/)).toBeInTheDocument()
  })

  it('keeps the last cards when a refresh fails', async () => {
    const get = vi.fn().mockResolvedValueOnce(closedList([closedException()])).mockRejectedValue(new ApiError(500, null))
    const { client } = renderWithApi(<ClosedExceptionsTab />, { get: get as unknown as Api['get'], post: vi.fn() })
    await screen.findByRole('article')

    await client.refetchQueries({ queryKey: ['closedExceptions'] })

    expect(screen.getByRole('article')).toBeInTheDocument()
  })

  it('offers no way to change a closed exception', async () => {
    renderWithApi(<ClosedExceptionsTab />, apiFor([closedException()]))

    await screen.findByRole('article')
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})

describe('Exceptions tabs', () => {
  const page = (
    <Routes>
      <Route path="exceptions" element={<ExceptionsPage />}>
        <Route index element={<OpenExceptionsTab />} />
        <Route path="closed" element={<ClosedExceptionsTab />} />
      </Route>
    </Routes>
  )

  it('shows Open and Closed as links, the open one current, with the open count', async () => {
    renderWithApi(page, apiFor([closedException()]), '/exceptions')

    const tabs = within(await screen.findByRole('navigation', { name: 'Exceptions views' }))
    expect(tabs.getByRole('link', { name: /^Open/ })).toHaveAttribute('aria-current', 'page')
    expect(tabs.getByRole('link', { name: 'Closed' })).not.toHaveAttribute('aria-current')
    expect(await tabs.findByLabelText('1 open')).toBeInTheDocument()
  })

  it('switches to the closed cards and back', async () => {
    renderWithApi(page, apiFor([closedException()]), '/exceptions')
    await userEvent.click(await screen.findByRole('link', { name: 'Closed' }))

    expect(await screen.findByRole('article')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Closed' })).toHaveAttribute('aria-current', 'page')

    await userEvent.click(screen.getByRole('link', { name: /^Open/ }))
    expect(await screen.findByText('Delay reason waiting for verification')).toBeInTheDocument()
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
  })

  it('opens the Closed tab straight from its address', async () => {
    renderWithApi(page, apiFor([closedException()]), '/exceptions/closed')

    expect(await screen.findByRole('article')).toBeInTheDocument()
  })
})
