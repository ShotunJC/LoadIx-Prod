import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { InstructionsPage } from './instructions-page'

const mocks = vi.hoisted(() => ({ request: vi.fn() }))

vi.mock('@/auth/auth-context', () => ({
  useAuth: () => ({
    user: { permissions: ['Trip.View', 'Trip.Manage'] },
    request: mocks.request,
  }),
}))

describe('InstructionsPage', () => {
  it('shows quantity progress and opens the creation wizard', async () => {
    mocks.request.mockImplementation((input: RequestInfo | URL) => {
      const url =
        typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.toString()
            : input.url
      if (url.startsWith('/api/v1/load-instructions?')) {
        return Promise.resolve(
          response({
            items: [
              {
                id: '72000000-0000-0000-0000-000000000001',
                instructionNumber: 'LI-DEMO-001',
                customerId: '12000000-0000-0000-0000-000000000001',
                customerName: 'Synthetic Customer',
                transportContractId: '71000000-0000-0000-0000-000000000001',
                contractNumber: 'CTR-DEMO',
                commodityName: 'Aggregate Stone',
                requestedQuantity: 30,
                completedQuantity: 10,
                remainingQuantity: 20,
                quantityUnit: 'Tonnes',
                collectionWindowStartUtc: '2026-08-01T06:00:00Z',
                priority: 'High',
                status: 'InProgress',
                generatedTripCount: 2,
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
          items: [],
          page: 1,
          pageSize: 10,
          totalCount: 0,
          totalPages: 0,
        }),
      )
    })
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
    render(
      <QueryClientProvider client={queryClient}>
        <InstructionsPage />
      </QueryClientProvider>,
    )

    expect(await screen.findByText('LI-DEMO-001')).toBeInTheDocument()
    expect(screen.getByText('10 / 30 Tonnes')).toBeInTheDocument()
    await userEvent.click(
      screen.getByRole('button', { name: 'New instruction' }),
    )
    expect(
      screen.getByRole('dialog', { name: 'Create load instruction' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Instruction number')).toBeInTheDocument()
    expect(screen.getByLabelText('Transport contract')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Help for Instruction number' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Help for Transport contract' }),
    ).toBeInTheDocument()
  })
})

function response(payload: unknown) {
  return {
    ok: true,
    json: () => Promise.resolve(payload),
  }
}
