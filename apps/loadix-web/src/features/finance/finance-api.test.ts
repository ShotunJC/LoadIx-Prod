import { describe, expect, it, vi } from 'vitest'
import {
  addTripCost,
  createInvoiceBatch,
  downloadInvoiceBatchExport,
  getTaxConfiguration,
  listInvoiceReady,
  previewInvoiceBatch,
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
