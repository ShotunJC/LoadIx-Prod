import { describe, expect, it, vi } from 'vitest'
import {
  addTripCost,
  createInvoiceBatch,
  createPayrollBatch,
  downloadInvoiceBatchExport,
  downloadPayrollBatchExport,
  getTaxConfiguration,
  listInvoiceReady,
  previewInvoiceBatch,
  previewPayrollBatch,
  saveTaxConfiguration,
} from './finance-api'

describe('finance API client', () => {
  it('loads tax configuration and invoice-ready trips with typed parsing', async () => {
    const request = vi.fn((input: RequestInfo | URL) => {
      const url = toUrl(input)
      if (url.endsWith('/tax-configuration')) {
        return json({
          id: '10000000-0000-0000-0000-000000000001',
          defaultTaxTreatment: 'StandardRated',
          taxRatePercent: 15,
          isReviewed: true,
          reviewedByUserId: '20000000-0000-0000-0000-000000000001',
          reviewedAtUtc: '2026-07-30T10:00:00+00:00',
          version: 'AAAA',
        })
      }
      return json({
        items: [
          {
            financialsId: '30000000-0000-0000-0000-000000000001',
            tripId: '40000000-0000-0000-0000-000000000001',
            tripNumber: 'TRIP-001',
            customerId: '50000000-0000-0000-0000-000000000001',
            customerName: 'Synthetic Customer',
            transportContractId: '60000000-0000-0000-0000-000000000001',
            contractNumber: 'CTR-001',
            billableTonnes: 12.345,
            taxExclusiveAmount: 1000,
            taxAmount: 150,
            total: 1150,
            currencyCode: 'ZAR',
            completedAtUtc: '2026-07-30T10:00:00+00:00',
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
        totalPages: 1,
      })
    })

    expect((await getTaxConfiguration(request)).taxRatePercent).toBe(15)
    const ready = await listInvoiceReady(request, { page: 1, search: 'TRIP' })
    expect(ready.items).toHaveLength(1)
    expect(ready.items[0]?.tripNumber).toBe('TRIP-001')
  })

  it('sends reviewed tax configuration, cost entries and batch selections', async () => {
    const request = vi.fn(() =>
      json({
        id: '10000000-0000-0000-0000-000000000001',
        defaultTaxTreatment: 'StandardRated',
        taxRatePercent: 15,
        isReviewed: true,
        reviewedByUserId: '20000000-0000-0000-0000-000000000001',
        reviewedAtUtc: '2026-07-30T10:00:00+00:00',
        version: 'BBBB',
      }),
    )

    await saveTaxConfiguration(request, {
      defaultTaxTreatment: 'StandardRated',
      taxRatePercent: 15,
      version: 'AAAA',
    })

    expect(request).toHaveBeenCalledWith(
      '/api/v1/finance/tax-configuration',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({
          defaultTaxTreatment: 'StandardRated',
          taxRatePercent: 15,
          version: 'AAAA',
        }),
      }),
    )

    const tripRequest = vi.fn(() => json(tripFinancials()))
    await addTripCost(tripRequest, '40000000-0000-0000-0000-000000000001', {
      type: 'FuelActual',
      amount: 250.125,
      description: 'Synthetic fuel slip',
      isAdjustment: false,
      version: 'AAAA',
    })
    expect(tripRequest).toHaveBeenCalledWith(
      '/api/v1/finance/trips/40000000-0000-0000-0000-000000000001/costs',
      expect.objectContaining({ method: 'POST' }),
    )

    const batchRequest = vi.fn(() =>
      json([
        {
          customerId: '50000000-0000-0000-0000-000000000001',
          customerName: 'Synthetic Customer',
          transportContractId: '60000000-0000-0000-0000-000000000001',
          contractNumber: 'CTR-001',
          currencyCode: 'ZAR',
          taxExclusiveAmount: 1000,
          taxAmount: 150,
          total: 1150,
          lines: [],
        },
      ]),
    )
    await previewInvoiceBatch(batchRequest, [
      '30000000-0000-0000-0000-000000000001',
    ])
    expect(batchRequest).toHaveBeenCalledWith(
      '/api/v1/finance/invoice-batches/preview',
      expect.objectContaining({ method: 'POST' }),
    )

    const createRequest = vi.fn(() => json(invoiceBatch()))
    await createInvoiceBatch(createRequest, [
      '30000000-0000-0000-0000-000000000001',
    ])
    expect(createRequest).toHaveBeenCalledWith(
      '/api/v1/finance/invoice-batches',
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('downloads invoice exports through the authenticated request function', async () => {
    const request = vi.fn(() =>
      Promise.resolve(
        new Response('batch,amount\nINV-001,100.00\n', {
          status: 200,
          headers: { 'Content-Type': 'text/csv' },
        }),
      ),
    )

    const result = await downloadInvoiceBatchExport(
      request,
      '70000000-0000-0000-0000-000000000001',
      'csv',
    )

    expect(request).toHaveBeenCalledWith(
      '/api/v1/finance/invoice-batches/70000000-0000-0000-0000-000000000001/export.csv',
    )
    expect(result).toBeInstanceOf(Blob)
    expect(result.type).toBe('text/csv')
    expect(result.size).toBeGreaterThan(0)
  })

  it('downloads pastel invoice exports through the authenticated request function', async () => {
    const request = vi.fn(() =>
      Promise.resolve(
        new Response('HEADER\nDETAIL\n', {
          status: 200,
          headers: { 'Content-Type': 'text/csv' },
        }),
      ),
    )

    const result = await downloadInvoiceBatchExport(
      request,
      '70000000-0000-0000-0000-000000000001',
      'pastel',
    )

    expect(request).toHaveBeenCalledWith(
      '/api/v1/finance/invoice-batches/70000000-0000-0000-0000-000000000001/export-pastel.csv',
    )
    expect(result).toBeInstanceOf(Blob)
    expect(result.type).toBe('text/csv')
  })

  it('previews and creates payroll batches with typed validation', async () => {
    const previewRequest = vi.fn(() =>
      json({
        periodStartDate: '2026-08-01',
        periodEndDate: '2026-08-31',
        frequency: 'Monthly',
        currencyCode: 'ZAR',
        eligibleDriversCount: 1,
        totalDeductionsCount: 1,
        totalDeductionsAmount: 500,
        lines: [
          {
            driverId: '80000000-0000-0000-0000-000000000001',
            driverEmployeeNumber: 'EMP001',
            driverName: 'Sipho Zulu',
            deductionId: '90000000-0000-0000-0000-000000000001',
            deductionNumber: 'DED-001',
            transactionCode: '5010',
            description: 'Uniform deduction',
            installmentAmount: 500,
            currentBalance: 1500,
            balanceAfterDeduction: 1000,
          },
        ],
      }),
    )

    const preview = await previewPayrollBatch(previewRequest, {
      periodStartDate: '2026-08-01',
      periodEndDate: '2026-08-31',
      frequency: 'Monthly',
    })

    expect(preview.eligibleDriversCount).toBe(1)
    expect(preview.lines[0]?.driverEmployeeNumber).toBe('EMP001')

    const createRequest = vi.fn(() =>
      json({
        id: 'a0000000-0000-0000-0000-000000000001',
        tenantId: 'b0000000-0000-0000-0000-000000000001',
        batchNumber: 'PAY-202608-01',
        periodStartDate: '2026-08-01',
        periodEndDate: '2026-08-31',
        frequency: 'Monthly',
        currencyCode: 'ZAR',
        status: 'Draft',
        totalDeductionsAmount: 500,
        createdAtUtc: '2026-08-31T10:00:00+00:00',
        approvedAtUtc: null,
        approvedByUserId: null,
        exportedAtUtc: null,
        exportedByUserId: null,
        lines: [
          {
            id: 'c0000000-0000-0000-0000-000000000001',
            driverId: '80000000-0000-0000-0000-000000000001',
            driverEmployeeNumber: 'EMP001',
            driverName: 'Sipho Zulu',
            deductionId: '90000000-0000-0000-0000-000000000001',
            transactionCode: '5010',
            description: 'Uniform deduction',
            amount: 500,
            balanceAfterDeduction: 1000,
          },
        ],
      }),
    )

    const batch = await createPayrollBatch(createRequest, {
      batchNumber: 'PAY-202608-01',
      periodStartDate: '2026-08-01',
      periodEndDate: '2026-08-31',
      frequency: 'Monthly',
      currencyCode: 'ZAR',
    })

    expect(batch.batchNumber).toBe('PAY-202608-01')
    expect(batch.lines).toHaveLength(1)
  })

  it('downloads pastel and generic payroll exports', async () => {
    const request = vi.fn(() =>
      Promise.resolve(
        new Response('EMP001,5010,500.00\n', {
          status: 200,
          headers: { 'Content-Type': 'text/csv' },
        }),
      ),
    )

    const pastel = await downloadPayrollBatchExport(
      request,
      'a0000000-0000-0000-0000-000000000001',
      'pastel',
    )
    expect(request).toHaveBeenCalledWith(
      '/api/v1/payroll/batches/a0000000-0000-0000-0000-000000000001/export-pastel.csv',
    )
    expect(pastel.size).toBeGreaterThan(0)

    const generic = await downloadPayrollBatchExport(
      request,
      'a0000000-0000-0000-0000-000000000001',
      'generic',
    )
    expect(request).toHaveBeenCalledWith(
      '/api/v1/payroll/batches/a0000000-0000-0000-0000-000000000001/export.csv',
    )
    expect(generic.size).toBeGreaterThan(0)
  })
})

function toUrl(input: RequestInfo | URL) {
  return typeof input === 'string'
    ? input
    : input instanceof URL
      ? input.href
      : input.url
}

function json(value: unknown) {
  return Promise.resolve(
    new Response(JSON.stringify(value), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }),
  )
}

function tripFinancials() {
  return {
    id: '30000000-0000-0000-0000-000000000001',
    tripId: '40000000-0000-0000-0000-000000000001',
    tripNumber: 'TRIP-001',
    customerId: '50000000-0000-0000-0000-000000000001',
    customerName: 'Synthetic Customer',
    transportContractId: '60000000-0000-0000-0000-000000000001',
    contractNumber: 'CTR-001',
    currencyCode: 'ZAR',
    rateType: 'FixedPerTrip',
    billableTonnes: 0,
    subtotal: 1000,
    surcharge: 0,
    taxExclusiveAmount: 1000,
    taxTreatment: 'StandardRated',
    taxRatePercent: 15,
    taxAmount: 150,
    total: 1150,
    calculatedAtUtc: '2026-07-30T10:00:00+00:00',
    calculationVersion: 'trip-financials-v1',
    approvalStatus: 'Pending',
    invoiceBatchId: null,
    profitability: {
      expectedRevenue: { amount: 1000, currencyCode: 'ZAR' },
      totalCost: { amount: 250.125, currencyCode: 'ZAR' },
      grossMargin: { amount: 749.875, currencyCode: 'ZAR' },
      marginPercentage: 74.9875,
      costPerTonne: 0,
      revenuePerTonne: 0,
    },
    costs: [],
    version: 'AAAA',
  }
}

function invoiceBatch() {
  return {
    id: '70000000-0000-0000-0000-000000000001',
    batchNumber: 'INV-20260730-0001',
    customerId: '50000000-0000-0000-0000-000000000001',
    customerName: 'Synthetic Customer',
    transportContractId: '60000000-0000-0000-0000-000000000001',
    contractNumber: 'CTR-001',
    currencyCode: 'ZAR',
    status: 'Draft',
    taxExclusiveAmount: 1000,
    taxAmount: 150,
    total: 1150,
    createdAtUtc: '2026-07-30T10:00:00+00:00',
    approvedAtUtc: null,
    approvedByUserId: null,
    exportedAtUtc: null,
    exportedByUserId: null,
    lines: [],
    version: 'AAAA',
  }
}
