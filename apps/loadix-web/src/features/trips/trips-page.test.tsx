import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { TripsPage } from './trips-page'

const mocks = vi.hoisted(() => ({ request: vi.fn() }))

vi.mock('@/auth/auth-context', () => ({
  useAuth: () => ({
    user: {
      permissions: [
        'Trip.View',
        'Trip.Manage',
        'Trip.Dispatch',
        'Trip.Complete',
        'Document.View',
        'Document.Manage',
      ],
    },
    request: mocks.request,
  }),
}))

describe('TripsPage', () => {
  it('opens an assigned trip with timeline and dispatch control', async () => {
    const trip = {
      id: '73000000-0000-0000-0000-000000000001',
      tripNumber: 'LI-DEMO-T001',
      loadInstructionId: '73000000-0000-0000-0000-000000000002',
      instructionNumber: 'LI-DEMO',
      customerId: '73000000-0000-0000-0000-000000000003',
      customerName: 'Synthetic Customer',
      routeReference: 'MINE-TO-DEPOT',
      commodityName: 'Aggregate Stone',
      plannedQuantity: 30,
      quantityUnit: 'Tonnes',
      plannedCollectionAtUtc: '2026-08-01T06:00:00Z',
      plannedDeliveryAtUtc: '2026-08-01T14:00:00Z',
      driverName: 'Demo Driver 01',
      vehicleReference: 'TRK-001',
      status: 'Assigned',
      version: 'AAAA',
    }
    mocks.request.mockImplementation((input: RequestInfo | URL) => {
      const url =
        typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.toString()
            : input.url
      if (url.startsWith('/api/v1/trips?')) {
        return Promise.resolve(
          response({
            items: [{ ...trip, hasException: false }],
            page: 1,
            pageSize: 15,
            totalCount: 1,
            totalPages: 1,
          }),
        )
      }
      if (url.startsWith('/api/v1/documents?')) {
        return Promise.resolve(response([]))
      }
      return Promise.resolve(
        response({
          ...trip,
          transportContractId: '73000000-0000-0000-0000-000000000004',
          contractNumber: 'CTR-DEMO',
          contractVersion: 'AQID',
          contractRouteId: '73000000-0000-0000-0000-000000000005',
          sourceLocationName: 'Demo Mine',
          destinationLocationName: 'Demo Depot',
          commodityId: '73000000-0000-0000-0000-000000000006',
          rateSnapshot: {
            rateId: '73000000-0000-0000-0000-000000000007',
            rateType: 'FixedPerTrip',
            effectiveFrom: '2026-01-01',
            effectiveTo: '2026-12-31',
            fixedAmount: 1000,
            baseFee: null,
            perTonneAmount: null,
            minimumCharge: null,
            fuelSurchargePercent: null,
            roundingDecimalPlaces: 2,
            roundingMode: 'AwayFromZero',
            currencyCode: 'ZAR',
          },
          driverId: '73000000-0000-0000-0000-000000000008',
          vehicleId: '73000000-0000-0000-0000-000000000009',
          trailerIds: [],
          trailerReferences: [],
          operationalNotes: '',
          cancellationOrRejectionReason: '',
          sourceGrossWeight: null,
          sourceTareWeight: null,
          sourceNetWeight: null,
          sourceWeighbridgeReference: null,
          destinationGrossWeight: null,
          destinationTareWeight: null,
          destinationNetWeight: null,
          destinationWeighbridgeReference: null,
          isReconciled: false,
          hasRequiredDocuments: false,
          transitions: [
            {
              id: '73000000-0000-0000-0000-000000000010',
              previousStatus: 'Planned',
              newStatus: 'Assigned',
              actorUserId: '73000000-0000-0000-0000-000000000011',
              occurredAtUtc: '2026-08-01T05:00:00Z',
              reason: null,
              isManualOverride: false,
            },
          ],
          assignments: [],
        }),
      )
    })
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
    render(
      <QueryClientProvider client={queryClient}>
        <TripsPage />
      </QueryClientProvider>,
    )

    await userEvent.click(await screen.findByText('LI-DEMO-T001'))

    expect(
      await screen.findByRole('dialog', { name: 'LI-DEMO-T001' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Dispatch trip' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Planned → Assigned')).toBeInTheDocument()
    expect(screen.getByText('Demo Mine → Demo Depot')).toBeInTheDocument()
  })
})

function response(payload: unknown) {
  return {
    ok: true,
    json: () => Promise.resolve(payload),
  }
}
