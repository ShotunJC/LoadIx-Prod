import { describe, expect, it, vi } from 'vitest'
import {
  approveDeduction,
  cancelDeduction,
  createDeduction,
  getDriverDisciplinarySummary,
  issueWarning,
  revokeWarning,
} from './driver-disciplinary-api'

describe('driver disciplinary API client', () => {
  it('loads driver disciplinary summary with typed parsing', async () => {
    const request = vi.fn(() =>
      json({
        driverId: '10000000-0000-0000-0000-000000000001',
        driverName: 'Mandla Sithole',
        employeeNumber: 'EMP042',
        activeWarningsCount: 1,
        totalWarningsCount: 2,
        totalActiveDeductionsBalance: 1200,
        activeWarnings: [
          {
            id: '20000000-0000-0000-0000-000000000001',
            tenantId: '30000000-0000-0000-0000-000000000001',
            driverId: '10000000-0000-0000-0000-000000000001',
            warningNumber: 'WRN-2026-001',
            warningType: 2, // FirstWritten
            category: 1, // Speeding
            status: 1, // Active
            incidentDateUtc: '2026-08-01T10:00:00Z',
            issuedDateUtc: '2026-08-02T10:00:00Z',
            expiryDateUtc: '2027-02-02T10:00:00Z',
            description: 'Exceeded 90km/h on N3 corridor',
            issuedByUserId: '40000000-0000-0000-0000-000000000001',
          },
        ],
        activeDeductions: [
          {
            id: '50000000-0000-0000-0000-000000000001',
            tenantId: '30000000-0000-0000-0000-000000000001',
            driverId: '10000000-0000-0000-0000-000000000001',
            deductionNumber: 'DED-2026-001',
            category: 1, // Traffic fine
            currencyCode: 'ZAR',
            totalAmount: 1500,
            installmentAmount: 500,
            remainingBalance: 1200,
            effectiveDate: '2026-08-01',
            description: 'AARTO speeding camera fine on N3',
            status: 3, // Active
            createdAtUtc: '2026-08-02T10:00:00Z',
          },
        ],
      }),
    )

    const summary = await getDriverDisciplinarySummary(
      request,
      '10000000-0000-0000-0000-000000000001',
    )

    expect(request).toHaveBeenCalledWith(
      '/api/v1/drivers/10000000-0000-0000-0000-000000000001/disciplinary-summary',
    )
    expect(summary.driverName).toBe('Mandla Sithole')
    expect(summary.employeeNumber).toBe('EMP042')
    expect(summary.activeWarnings).toHaveLength(1)
    expect(summary.activeDeductions).toHaveLength(1)
  })

  it('issues and revokes disciplinary warnings', async () => {
    const issueRequest = vi.fn(() =>
      json({
        id: '20000000-0000-0000-0000-000000000001',
        tenantId: '30000000-0000-0000-0000-000000000001',
        driverId: '10000000-0000-0000-0000-000000000001',
        warningNumber: 'WRN-2026-002',
        warningType: 1,
        category: 2,
        status: 1,
        incidentDateUtc: '2026-08-05T08:00:00Z',
        issuedDateUtc: '2026-08-05T10:00:00Z',
        expiryDateUtc: '2027-02-05T10:00:00Z',
        description: 'Fuel reconciliation discrepancy',
        issuedByUserId: '40000000-0000-0000-0000-000000000001',
      }),
    )

    const warning = await issueWarning(
      issueRequest,
      '10000000-0000-0000-0000-000000000001',
      {
        warningNumber: 'WRN-2026-002',
        warningType: 1,
        category: 2,
        incidentDateUtc: '2026-08-05T08:00:00Z',
        issuedDateUtc: '2026-08-05T10:00:00Z',
        expiryDateUtc: '2027-02-05T10:00:00Z',
        description: 'Fuel reconciliation discrepancy',
      },
    )

    expect(issueRequest).toHaveBeenCalledWith(
      '/api/v1/drivers/10000000-0000-0000-0000-000000000001/warnings',
      expect.objectContaining({ method: 'POST' }),
    )
    expect(warning.warningNumber).toBe('WRN-2026-002')

    const revokeRequest = vi.fn(() =>
      json({
        ...warning,
        status: 3, // Revoked
        revocationReason: 'Telematics sensor calibration error verified',
      }),
    )

    const revoked = await revokeWarning(
      revokeRequest,
      warning.id,
      'Telematics sensor calibration error verified',
    )
    expect(revokeRequest).toHaveBeenCalledWith(
      `/api/v1/warnings/${warning.id}/revoke`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          reason: 'Telematics sensor calibration error verified',
        }),
      }),
    )
    expect(revoked.status).toBe(3)
  })

  it('creates, approves, and cancels driver deductions', async () => {
    const createRequest = vi.fn(() =>
      json({
        id: '50000000-0000-0000-0000-000000000001',
        tenantId: '30000000-0000-0000-0000-000000000001',
        driverId: '10000000-0000-0000-0000-000000000001',
        deductionNumber: 'DED-2026-003',
        category: 2,
        currencyCode: 'ZAR',
        totalAmount: 3000,
        installmentAmount: 1000,
        remainingBalance: 3000,
        effectiveDate: '2026-08-01',
        description: 'Trailer wing mirror damage',
        status: 1, // Draft
        createdAtUtc: '2026-08-05T10:00:00Z',
      }),
    )

    const created = await createDeduction(
      createRequest,
      '10000000-0000-0000-0000-000000000001',
      {
        deductionNumber: 'DED-2026-003',
        category: 2,
        currencyCode: 'ZAR',
        totalAmount: 3000,
        installmentAmount: 1000,
        effectiveDate: '2026-08-01',
        description: 'Trailer wing mirror damage',
      },
    )

    expect(created.status).toBe(1)

    const approveRequest = vi.fn(() =>
      json({
        ...created,
        status: 2, // Approved
      }),
    )

    const approved = await approveDeduction(approveRequest, created.id)
    expect(approveRequest).toHaveBeenCalledWith(
      `/api/v1/deductions/${created.id}/approve`,
      expect.objectContaining({ method: 'POST' }),
    )
    expect(approved.status).toBe(2)

    const cancelRequest = vi.fn(() =>
      json({
        ...approved,
        status: 5, // Cancelled
      }),
    )

    const cancelled = await cancelDeduction(
      cancelRequest,
      created.id,
      'Insurance covered the replacement mirror',
    )
    expect(cancelRequest).toHaveBeenCalledWith(
      `/api/v1/deductions/${created.id}/cancel`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          reason: 'Insurance covered the replacement mirror',
        }),
      }),
    )
    expect(cancelled.status).toBe(5)
  })
})

function json(value: unknown) {
  return Promise.resolve(
    new Response(JSON.stringify(value), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }),
  )
}
