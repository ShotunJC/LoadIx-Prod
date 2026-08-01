import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { ReportingPage } from './reporting-page'

const mocks = vi.hoisted(() => ({ request: vi.fn() }))

vi.mock('@/auth/auth-context', () => ({
  useAuth: () => ({ request: mocks.request }),
}))

function response(payload: unknown) {
  return { ok: true, json: () => Promise.resolve(payload) }
}

function requestUrl(input: RequestInfo | URL) {
  return typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.href
      : input.url
}

function requestCalls() {
  return mocks.request.mock.calls as unknown as Array<[RequestInfo | URL]>
}

describe('ReportingPage', () => {
  it('renders all KPIs and sends filters and pagination to the server', async () => {
    mocks.request.mockImplementation((input: RequestInfo | URL) => {
      const url = requestUrl(input)
      if (url.includes('/branches')) {
        return Promise.resolve(
          response({
            items: [
              {
                id: '12000000-0000-0000-0000-000000000001',
                reference: 'BR-1',
                name: 'North branch',
                isActive: true,
                version: 'v1',
                modifiedAtUtc: '',
              },
            ],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }),
        )
      }
      if (url.includes('/customers')) {
        return Promise.resolve(
          response({
            items: [
              {
                id: '12000000-0000-0000-0000-000000000002',
                reference: 'CUS-1',
                tradingName: 'North customer',
                isActive: true,
                version: 'v1',
                modifiedAtUtc: '',
              },
            ],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }),
        )
      }
      if (url.includes('/dashboard')) {
        return Promise.resolve(
          response({
            activeTrips: 1,
            completedTripsToday: 2,
            tonnesDeliveredToday: 3.125,
            awaitingReconciliation: 4,
            tripsOutsideTolerance: 5,
            delayedTrips: 6,
            fleetUtilisationPercent: 7.5,
            revenue: 800,
            estimatedGrossMargin: 300,
            customerConcentrationPercent: 90,
            upcomingLicenceExpiries: 2,
            generatedAtUtc: '2026-08-01T08:00:00Z',
            timeZone: 'Africa/Johannesburg',
            currencyCode: 'ZAR',
          }),
        )
      }
      return Promise.resolve(
        response({
          items: [
            { tripId: 'trip-1', tripNumber: 'TRIP-1', status: 'Completed' },
          ],
          page: url.includes('page=2') ? 2 : 1,
          pageSize: 50,
          totalCount: 51,
          totalPages: 2,
          generatedAtUtc: '2026-08-01T08:00:00Z',
          timeZone: 'Africa/Johannesburg',
        }),
      )
    })
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
    render(
      <QueryClientProvider client={queryClient}>
        <ReportingPage />
      </QueryClientProvider>,
    )

    expect(await screen.findByText('Active trips')).toBeInTheDocument()
    expect(screen.getByText('Customer concentration')).toBeInTheDocument()
    expect(screen.getByText('Upcoming licence expiries')).toBeInTheDocument()

    const user = userEvent.setup()
    await user.selectOptions(
      screen.getByLabelText('Branch'),
      '12000000-0000-0000-0000-000000000001',
    )
    await user.selectOptions(
      screen.getByLabelText('Customer'),
      '12000000-0000-0000-0000-000000000002',
    )
    await user.clear(screen.getByLabelText('Date from'))
    await user.type(screen.getByLabelText('Date from'), '2026-08-01')
    await user.clear(screen.getByLabelText('Date to'))
    await user.type(screen.getByLabelText('Date to'), '2026-08-02')
    await waitFor(() => {
      expect(
        requestCalls().some(
          ([input]) =>
            requestUrl(input).includes('from=2026-08-01') &&
            requestUrl(input).includes('to=2026-08-02') &&
            requestUrl(input).includes(
              'branchId=12000000-0000-0000-0000-000000000001',
            ) &&
            requestUrl(input).includes(
              'customerId=12000000-0000-0000-0000-000000000002',
            ),
        ),
      ).toBe(true)
    })

    await user.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => {
      expect(
        requestCalls().some(
          ([input]) =>
            requestUrl(input).includes('/reports/trip-register') &&
            requestUrl(input).includes('page=2'),
        ),
      ).toBe(true)
    })
  })
})
