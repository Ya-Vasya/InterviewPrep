import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { Layout } from './Layout'

vi.mock('../api/client', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/client')>()),
  api: { getAiStatus: vi.fn() },
}))

const getAiStatus = vi.mocked(api.getAiStatus)

function renderLayout(enabled: boolean, handle?: { wide: boolean }) {
  getAiStatus.mockResolvedValue({ enabled, usedToday: 0, dailyCap: 50 })
  const router = createMemoryRouter([{ element: <Layout />, children: [{ path: '/', handle, element: <p>Home</p> }] }])
  render(
    <QueryClientProvider client={new QueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

beforeEach(() => vi.clearAllMocks())

describe('Layout', () => {
  it('links to Generate only when AI is enabled', async () => {
    renderLayout(true)

    expect(await screen.findByRole('link', { name: 'Generate' })).toHaveAttribute('href', '/generate')
    expect(screen.getByRole('link', { name: 'Study' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Questions' })).toBeInTheDocument()
  })

  it('hides the Generate link when AI is disabled', async () => {
    renderLayout(false)

    await screen.findByText('Home')
    await vi.waitFor(() => expect(getAiStatus).toHaveBeenCalled())
    expect(screen.queryByRole('link', { name: 'Generate' })).not.toBeInTheDocument()
  })

  it('uses the wide layout only for routes that ask for it', async () => {
    renderLayout(false, { wide: true })

    expect((await screen.findByText('Home')).closest('main')).toHaveClass('wide')
  })

  it('uses the normal width otherwise', async () => {
    renderLayout(false)

    expect((await screen.findByText('Home')).closest('main')).not.toHaveClass('wide')
  })
})
