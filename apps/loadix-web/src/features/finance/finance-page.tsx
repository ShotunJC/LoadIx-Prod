import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  CheckCircle2,
  Download,
  FileSpreadsheet,
  FileText,
  Receipt,
  RefreshCcw,
} from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  approveInvoiceBatch,
  createInvoiceBatch,
  downloadInvoiceBatchExport,
  getTaxConfiguration,
  listInvoiceBatches,
  listInvoiceReady,
  markInvoiceBatchExported,
  previewInvoiceBatch,
  saveTaxConfiguration,
  type InvoiceBatchListItem,
  type InvoiceBatchPreview,
  type InvoiceReadyTrip,
} from '@/features/finance/finance-api'

const taxTreatments = [
  'StandardRated',
  'ZeroRated',
  'Exempt',
  'OutOfScope',
] as const

function money(value: number, currencyCode: string) {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currencyCode,
  }).format(value)
}

function dateTime(value: string | null) {
  return value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—'
}

export function FinancePage() {
  const auth = useAuth()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<string[]>([])
  const queryClient = useQueryClient()

  const canManage = auth.user?.permissions.includes('Finance.Manage') ?? false
  const ready = useQuery({
    queryKey: ['finance', 'invoice-ready', page, search],
    queryFn: () => listInvoiceReady(auth.request, { page, search }),
  })
  const batches = useQuery({
    queryKey: ['finance', 'invoice-batches'],
    queryFn: () => listInvoiceBatches(auth.request),
  })
  const preview = useMutation({
    mutationFn: () => previewInvoiceBatch(auth.request, selected),
  })
  const createBatch = useMutation({
    mutationFn: () => createInvoiceBatch(auth.request, selected),
    onSuccess: () => {
      setSelected([])
      void queryClient.invalidateQueries({ queryKey: ['finance'] })
    },
  })

  const selectedRows = useMemo(
    () =>
      ready.data?.items.filter((item) =>
        selected.includes(item.financialsId),
      ) ?? [],
    [ready.data?.items, selected],
  )

  return (
    <section className="space-y-6" aria-labelledby="finance-title">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-brand-700 text-xs font-bold tracking-[0.16em] uppercase">
            Finance control
          </p>
          <h1
            className="mt-1 text-3xl font-black tracking-tight text-slate-950"
            id="finance-title"
          >
            Financial preparation
          </h1>
          <p className="mt-1 max-w-3xl text-sm text-slate-600">
            Review tenant tax rules, select approved completed trips, prepare
            invoice batches, and export invoice-ready evidence without changing
            historical trip calculations.
          </p>
        </div>
        <div className="rounded-xl border bg-white px-4 py-3 text-right shadow-sm">
          <p className="text-xs font-semibold text-slate-500">
            Invoice-ready trips
          </p>
          <p className="text-2xl font-black text-slate-950">
            {ready.data?.totalCount ?? '—'}
          </p>
        </div>
      </header>

      <TaxConfigurationPanel canManage={canManage} />

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(22rem,0.65fr)]">
        <section
          aria-labelledby="invoice-ready-title"
          className="rounded-xl border bg-white shadow-sm"
        >
          <div className="border-b p-5">
            <div className="flex flex-wrap items-end justify-between gap-3">
              <div>
                <h2
                  className="text-lg font-black text-slate-950"
                  id="invoice-ready-title"
                >
                  Invoice-ready queue
                </h2>
                <p className="text-sm text-slate-600">
                  Completed, reconciled, financially approved trips that are not
                  already in an invoice batch.
                </p>
              </div>
              <label className="text-sm font-semibold text-slate-700">
                Search
                <input
                  className="mt-1 block min-h-11 rounded-md border px-3 font-normal"
                  onChange={(event) => {
                    setSearch(event.target.value)
                    setPage(1)
                  }}
                  placeholder="Trip, customer, contract"
                  value={search}
                />
              </label>
            </div>
          </div>

          {ready.isPending ? (
            <div className="space-y-3 p-5" aria-label="Loading invoice queue">
              {[1, 2, 3].map((item) => (
                <div
                  className="h-16 animate-pulse rounded-lg bg-slate-200"
                  key={item}
                />
              ))}
            </div>
          ) : ready.isError ? (
            <ErrorBlock message={ready.error.message} />
          ) : ready.data.items.length === 0 ? (
            <EmptyBlock
              title="No invoiceable trips yet"
              body="Complete, reconcile, and approve trip financials to populate this queue."
            />
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y text-sm">
                  <thead className="bg-slate-50 text-left text-xs font-bold tracking-wide text-slate-500 uppercase">
                    <tr>
                      <th className="px-4 py-3">Select</th>
                      <th className="px-4 py-3">Trip</th>
                      <th className="px-4 py-3">Customer</th>
                      <th className="px-4 py-3">Contract</th>
                      <th className="px-4 py-3 text-right">Billable</th>
                      <th className="px-4 py-3 text-right">Total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {ready.data.items.map((item) => (
                      <InvoiceReadyRow
                        item={item}
                        key={item.financialsId}
                        selected={selected.includes(item.financialsId)}
                        toggle={() => {
                          setSelected((value) =>
                            value.includes(item.financialsId)
                              ? value.filter((id) => id !== item.financialsId)
                              : [...value, item.financialsId],
                          )
                        }}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
              <footer className="flex flex-wrap items-center justify-between gap-3 border-t bg-slate-50 px-4 py-3 text-sm">
                <span>
                  Page {ready.data.page} of {Math.max(ready.data.totalPages, 1)}
                </span>
                <div className="flex gap-2">
                  <Button
                    disabled={page === 1}
                    onClick={() => {
                      setPage((value) => value - 1)
                    }}
                    type="button"
                    variant="outline"
                  >
                    Previous
                  </Button>
                  <Button
                    disabled={page >= ready.data.totalPages}
                    onClick={() => {
                      setPage((value) => value + 1)
                    }}
                    type="button"
                    variant="outline"
                  >
                    Next
                  </Button>
                </div>
              </footer>
            </>
          )}
        </section>

        <BatchBuilder
          canManage={canManage}
          createBatch={() => createBatch.mutateAsync()}
          createError={createBatch.error?.message}
          createPending={createBatch.isPending}
          preview={() => preview.mutateAsync()}
          previewError={preview.error?.message}
          previewPending={preview.isPending}
          previews={preview.data}
          selectedRows={selectedRows}
        />
      </div>

      <InvoiceBatchHistory
        batches={batches.data?.items ?? []}
        canManage={canManage}
        error={batches.error?.message}
        loading={batches.isPending}
        reload={() => {
          void batches.refetch()
        }}
      />
    </section>
  )
}

function TaxConfigurationPanel({ canManage }: { canManage: boolean }) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [treatment, setTreatment] = useState('StandardRated')
  const [rate, setRate] = useState('15')
  const query = useQuery({
    queryKey: ['finance', 'tax-configuration'],
    queryFn: async () => {
      const data = await getTaxConfiguration(auth.request)
      setTreatment(data.defaultTaxTreatment)
      setRate(String(data.taxRatePercent))
      return data
    },
  })
  const save = useMutation({
    mutationFn: () => {
      const model = {
        defaultTaxTreatment: treatment,
        taxRatePercent: Number(rate),
      }
      return saveTaxConfiguration(
        auth.request,
        query.data?.version ? { ...model, version: query.data.version } : model,
      )
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['finance', 'tax-configuration'],
      })
    },
  })

  return (
    <section className="rounded-xl border bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 className="text-lg font-black text-slate-950">
            Tenant tax configuration
          </h2>
          <p className="text-sm text-slate-600">
            Tax treatment is explicit and reviewable; LoadIx does not assume VAT
            policy for every transaction.
          </p>
          <p className="mt-1 text-xs text-slate-500">
            Last reviewed: {dateTime(query.data?.reviewedAtUtc ?? null)}
          </p>
        </div>
        <div className="grid gap-3 sm:grid-cols-[12rem_8rem_auto]">
          <label className="text-sm font-semibold text-slate-700">
            Default treatment
            <select
              className="mt-1 block min-h-11 w-full rounded-md border px-3 font-normal"
              disabled={!canManage}
              onChange={(event) => {
                setTreatment(event.target.value)
              }}
              value={treatment}
            >
              {taxTreatments.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-semibold text-slate-700">
            Rate %
            <input
              className="mt-1 block min-h-11 w-full rounded-md border px-3 font-normal"
              disabled={!canManage}
              min="0"
              onChange={(event) => {
                setRate(event.target.value)
              }}
              step="0.0001"
              type="number"
              value={rate}
            />
          </label>
          <Button
            className="self-end"
            disabled={!canManage || save.isPending}
            onClick={() => {
              save.mutate()
            }}
            type="button"
          >
            Save review
          </Button>
        </div>
      </div>
      {query.isError || save.isError ? (
        <p className="mt-3 rounded-lg bg-red-50 p-3 text-sm text-red-800">
          {query.error?.message ?? save.error?.message}
        </p>
      ) : null}
    </section>
  )
}

function InvoiceReadyRow({
  item,
  selected,
  toggle,
}: {
  item: InvoiceReadyTrip
  selected: boolean
  toggle: () => void
}) {
  return (
    <tr className={selected ? 'bg-brand-50' : undefined}>
      <td className="px-4 py-3">
        <input
          aria-label={`Select ${item.tripNumber}`}
          checked={selected}
          className="size-5 rounded border-slate-300"
          onChange={toggle}
          type="checkbox"
        />
      </td>
      <td className="px-4 py-3 font-bold text-slate-950">{item.tripNumber}</td>
      <td className="px-4 py-3">{item.customerName}</td>
      <td className="px-4 py-3">{item.contractNumber}</td>
      <td className="px-4 py-3 text-right">{item.billableTonnes}</td>
      <td className="px-4 py-3 text-right font-bold">
        {money(item.total, item.currencyCode)}
      </td>
    </tr>
  )
}

function BatchBuilder({
  canManage,
  createBatch,
  createError,
  createPending,
  preview,
  previewError,
  previewPending,
  previews,
  selectedRows,
}: {
  canManage: boolean
  createBatch: () => Promise<unknown>
  createError: string | undefined
  createPending: boolean
  preview: () => Promise<unknown>
  previewError: string | undefined
  previewPending: boolean
  previews: InvoiceBatchPreview[] | undefined
  selectedRows: InvoiceReadyTrip[]
}) {
  const firstCurrency = selectedRows[0]?.currencyCode ?? 'ZAR'
  const selectedTotal = selectedRows.reduce((sum, item) => sum + item.total, 0)
  return (
    <aside className="space-y-4">
      <section className="rounded-xl border bg-white p-5 shadow-sm">
        <h2 className="flex items-center gap-2 text-lg font-black text-slate-950">
          <Receipt size={20} /> Batch builder
        </h2>
        <p className="mt-1 text-sm text-slate-600">
          Select trips from a single customer, contract and currency, preview
          calculations, then create a draft batch for approval.
        </p>
        <div className="mt-4 rounded-lg bg-slate-50 p-4">
          <p className="text-xs font-semibold text-slate-500">
            Current selection
          </p>
          <p className="text-2xl font-black text-slate-950">
            {selectedRows.length} trips
          </p>
          <p className="text-sm font-bold text-slate-700">
            {money(selectedTotal, firstCurrency)}
          </p>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          <Button
            disabled={selectedRows.length === 0 || previewPending}
            onClick={() => {
              void preview()
            }}
            type="button"
            variant="outline"
          >
            Preview
          </Button>
          <Button
            disabled={!canManage || selectedRows.length === 0 || createPending}
            onClick={() => {
              void createBatch()
            }}
            type="button"
          >
            Create draft batch
          </Button>
        </div>
        {previewError || createError ? (
          <p className="mt-3 rounded-lg bg-red-50 p-3 text-sm text-red-800">
            {previewError ?? createError}
          </p>
        ) : null}
      </section>

      {previews?.map((preview) => (
        <section
          className="rounded-xl border bg-white p-5 shadow-sm"
          key={`${preview.customerId}-${preview.transportContractId}`}
        >
          <h3 className="font-black text-slate-950">{preview.customerName}</h3>
          <p className="text-sm text-slate-600">{preview.contractNumber}</p>
          <dl className="mt-4 grid grid-cols-3 gap-2 text-sm">
            <div>
              <dt className="text-xs font-semibold text-slate-500">Excl.</dt>
              <dd className="font-bold">
                {money(preview.taxExclusiveAmount, preview.currencyCode)}
              </dd>
            </div>
            <div>
              <dt className="text-xs font-semibold text-slate-500">Tax</dt>
              <dd className="font-bold">
                {money(preview.taxAmount, preview.currencyCode)}
              </dd>
            </div>
            <div>
              <dt className="text-xs font-semibold text-slate-500">Total</dt>
              <dd className="font-bold">
                {money(preview.total, preview.currencyCode)}
              </dd>
            </div>
          </dl>
        </section>
      ))}
    </aside>
  )
}

function InvoiceBatchHistory({
  batches,
  canManage,
  error,
  loading,
  reload,
}: {
  batches: InvoiceBatchListItem[]
  canManage: boolean
  error: string | undefined
  loading: boolean
  reload: () => void
}) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const approve = useMutation({
    mutationFn: (batch: InvoiceBatchListItem) =>
      approveInvoiceBatch(auth.request, batch),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['finance', 'invoice-batches'],
      })
    },
  })
  const exported = useMutation({
    mutationFn: (batch: InvoiceBatchListItem) =>
      markInvoiceBatchExported(auth.request, batch),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['finance', 'invoice-batches'],
      })
    },
  })
  const download = useMutation({
    mutationFn: ({
      batch,
      format,
    }: {
      batch: InvoiceBatchListItem
      format: 'csv' | 'pdf'
    }) => downloadInvoiceBatchExport(auth.request, batch.id, format),
    onSuccess: (blob, { batch, format }) => {
      const url = URL.createObjectURL(blob)
      const anchor = window.document.createElement('a')
      anchor.href = url
      anchor.download = `loadix-invoice-batch-${batch.id}.${format}`
      anchor.click()
      URL.revokeObjectURL(url)
    },
  })

  return (
    <section className="rounded-xl border bg-white shadow-sm">
      <div className="flex items-center justify-between gap-3 border-b p-5">
        <div>
          <h2 className="text-lg font-black text-slate-950">Export history</h2>
          <p className="text-sm text-slate-600">
            Draft, approved and exported invoice batches prepared by LoadIx.
          </p>
        </div>
        <Button onClick={reload} type="button" variant="outline">
          <RefreshCcw size={16} /> Refresh
        </Button>
      </div>
      {loading ? (
        <div className="p-5 text-sm text-slate-600">Loading batches…</div>
      ) : error ? (
        <ErrorBlock message={error} />
      ) : batches.length === 0 ? (
        <EmptyBlock
          title="No invoice batches yet"
          body="Create a draft batch from approved trips to begin finance review."
        />
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y text-sm">
            <thead className="bg-slate-50 text-left text-xs font-bold tracking-wide text-slate-500 uppercase">
              <tr>
                <th className="px-4 py-3">Batch</th>
                <th className="px-4 py-3">Customer</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Total</th>
                <th className="px-4 py-3">Dates</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {batches.map((batch) => (
                <tr key={batch.id}>
                  <td className="px-4 py-3 font-black text-slate-950">
                    {batch.batchNumber}
                  </td>
                  <td className="px-4 py-3">
                    {batch.customerName}
                    <span className="block text-xs text-slate-500">
                      {batch.contractNumber}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={batch.status} />
                  </td>
                  <td className="px-4 py-3 text-right font-bold">
                    {money(batch.total, batch.currencyCode)}
                  </td>
                  <td className="px-4 py-3 text-xs text-slate-600">
                    Created {dateTime(batch.createdAtUtc)}
                    <span className="block">
                      Exported {dateTime(batch.exportedAtUtc)}
                    </span>
                  </td>
                  <td className="space-x-2 px-4 py-3 text-right">
                    <Button
                      className="inline-flex min-h-9 items-center gap-1 rounded-md border px-3 text-xs font-bold"
                      disabled={download.isPending}
                      onClick={() => {
                        download.mutate({ batch, format: 'csv' })
                      }}
                      type="button"
                      variant="outline"
                    >
                      <FileSpreadsheet size={14} /> CSV
                    </Button>
                    <Button
                      className="inline-flex min-h-9 items-center gap-1 rounded-md border px-3 text-xs font-bold"
                      disabled={download.isPending}
                      onClick={() => {
                        download.mutate({ batch, format: 'pdf' })
                      }}
                      type="button"
                      variant="outline"
                    >
                      <FileText size={14} /> PDF
                    </Button>
                    {batch.status === 'Draft' ? (
                      <Button
                        className="min-h-9 px-3 text-xs"
                        disabled={!canManage || approve.isPending}
                        onClick={() => {
                          approve.mutate(batch)
                        }}
                        type="button"
                      >
                        Approve
                      </Button>
                    ) : null}
                    {batch.status === 'Approved' ? (
                      <Button
                        className="min-h-9 px-3 text-xs"
                        disabled={!canManage || exported.isPending}
                        onClick={() => {
                          exported.mutate(batch)
                        }}
                        type="button"
                        variant="outline"
                      >
                        <Download size={14} /> Mark exported
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {approve.isError || exported.isError || download.isError ? (
        <p className="m-5 rounded-lg bg-red-50 p-3 text-sm text-red-800">
          {approve.error?.message ??
            exported.error?.message ??
            download.error?.message}
        </p>
      ) : null}
    </section>
  )
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-bold text-emerald-800">
      <CheckCircle2 size={13} /> {status}
    </span>
  )
}

function ErrorBlock({ message }: { message: string }) {
  return (
    <div className="m-5 rounded-lg border border-red-200 bg-red-50 p-4 text-red-800">
      <p className="font-bold">Finance data could not be loaded</p>
      <p className="text-sm">{message}</p>
    </div>
  )
}

function EmptyBlock({ title, body }: { title: string; body: string }) {
  return (
    <div className="m-5 rounded-xl border border-dashed bg-slate-50 p-8 text-center">
      <p className="font-bold text-slate-950">{title}</p>
      <p className="mt-1 text-sm text-slate-600">{body}</p>
    </div>
  )
}
