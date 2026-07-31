import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { MasterDataPage } from './master-data-page'

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
}))

vi.mock('@/auth/auth-context', () => ({
  useAuth: () => ({
    user: {
      permissions: ['Customer.View', 'Customer.Manage'],
    },
    request: mocks.request,
  }),
}))

describe('MasterDataPage', () => {
  it('renders server data and opens the accessible customer editor', async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      json: () =>
        Promise.resolve({
          items: [
            {
              id: '12000000-0000-0000-0000-000000000001',
              reference: 'CUST-001',
              legalName: 'Synthetic Customer (Pty) Ltd',
              tradingName: 'Synthetic Customer',
              isActive: true,
              version: 'AAAA',
              modifiedAtUtc: '2026-07-28T08:00:00Z',
            },
          ],
          page: 1,
          pageSize: 10,
          totalCount: 1,
          totalPages: 1,
        }),
    })
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
    render(
      <QueryClientProvider client={queryClient}>
        <MasterDataPage module="customers" />
      </QueryClientProvider>,
    )

    expect(await screen.findByText('Synthetic Customer')).toBeInTheDocument()
    expect(screen.getByText('CUST-001')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Add customer' }))
    expect(
      screen.getByRole('dialog', { name: 'New customer' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Legal name')).toBeInTheDocument()
    expect(screen.getByLabelText('Contact category')).toBeInTheDocument()
  })
})
