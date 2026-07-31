import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { ContractsPage } from './contracts-page'

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
}))

vi.mock('@/auth/auth-context', () => ({
  useAuth: () => ({
    user: { permissions: ['Contract.View', 'Contract.Manage'] },
    request: mocks.request,
  }),
}))

describe('ContractsPage', () => {
  it('renders contracts and opens the accessible creation wizard', async () => {
    mocks.request.mockImplementation((input: RequestInfo | URL) => {
      const url =
        typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.toString()
            : input.url
      if (url.startsWith('/api/v1/contracts?')) {
        return Promise.resolve(
          response({
            items: [
              {
                id: '71000000-0000-0000-0000-000000000001',
                contractNumber: 'CTR-DEMO-001',
                name: 'Synthetic minerals transport',
                customerId: '12000000-0000-0000-0000-000000000001',
                customerName: 'Synthetic Customer',
                startDate: '2026-01-01',
                endDate: '2026-12-31',
                status: 'Draft',
                defaultCurrency: 'ZAR',
                routeCount: 1,
                commodityCount: 1,
                version: 'AAAA',
              },
            ],
            page: 1,
            pageSize: 10,
            totalCount: 1,
            totalPages: 1,
          }),
        )
      }
      return Promise.resolve(
        response({
          items: [
            {
              id: '12000000-0000-0000-0000-000000000001',
              reference: 'SYNTHETIC',
              tradingName: 'Synthetic Customer',
              isActive: true,
              version: 'AAAA',
              modifiedAtUtc: '2026-07-28T08:00:00Z',
            },
          ],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      )
    })
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
    render(
      <QueryClientProvider client={queryClient}>
        <ContractsPage />
      </QueryClientProvider>,
    )

    expect(await screen.findByText('CTR-DEMO-001')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'New contract' }))
    expect(
      screen.getByRole('dialog', { name: 'Create transport contract' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Contract number')).toBeInTheDocument()
    expect(screen.getByLabelText('Weight tolerance (%)')).toBeInTheDocument()
  })
})

function response(payload: unknown) {
  return {
    ok: true,
    json: () => Promise.resolve(payload),
  }
}
