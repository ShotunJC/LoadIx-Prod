import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertCircle,
  CheckCircle2,
  Download,
  FileSpreadsheet,
  Plus,
  RefreshCcw,
  X,
} from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  approvePayrollBatch,
  createPayrollBatch,
  downloadPayrollBatchExport,
  getPayrollBatch,
  listPayrollBatches,
  markPayrollBatchExported,
  previewPayrollBatch,
  type PayrollBatchDetail,
  type PayrollBatchListItem,
} from './finance-api'

function money(value: number, currencyCode = 'ZAR') {
  return new Intl.NumberFormat('en-ZA', {
    style: 'currency',
    currency: currencyCode,
  }).format(value)
}

function dateTime(value: string | null | undefined) {
  return value
    ? new Intl.DateTimeFormat('en-ZA', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—'
}

export function PayrollBatchesPanel({ canManage }: { canManage: boolean }) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [isCreating, setIsCreating] = useState(false)
  const [selectedBatchId, setSelectedBatchId] = useState<string | null>(null)

  const batches = useQuery({
    queryKey: ['finance', 'payroll-batches'],
    queryFn: () => listPayrollBatches(auth.request),
  })

  const batchDetail = useQuery({
    queryKey: ['finance', 'payroll-batches', selectedBatchId],
    queryFn: () =>
      selectedBatchId ? getPayrollBatch(auth.request, selectedBatchId) : null,
    enabled: Boolean(selectedBatchId),
  })

  const approve = useMutation({
    mutationFn: (batchId: string) => approvePayrollBatch(auth.request, batchId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['finance', 'payroll-batches'],
      })
    },
  })

  const markExported = useMutation({
    mutationFn: (batchId: string) =>
      markPayrollBatchExported(auth.request, batchId),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: ['finance', 'payroll-batches'],
      })
    },
  })

  const download = useMutation({
    mutationFn: ({
      batch,
      format,
    }: {
      batch: PayrollBatchListItem | PayrollBatchDetail
      format: 'pastel' | 'generic'
    }) => downloadPayrollBatchExport(auth.request, batch.id, format),
    onSuccess: (blob, { batch, format }) => {
      const url = URL.createObjectURL(blob)
      const anchor = window.document.createElement('a')
      anchor.href = url
      anchor.download =
        format === 'pastel'
          ? `pastel-payroll-batch-${batch.batchNumber}.csv`
          : `payroll-deductions-${batch.batchNumber}.csv`
      anchor.click()
      URL.revokeObjectURL(url)
    },
  })

  const activeBatch = batchDetail.data

  return (
    <section className="space-y-6" aria-labelledby="payroll-title">
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-xl border bg-white p-5 shadow-sm">
        <div>
          <h2 id="payroll-title" className="text-xl font-black text-slate-950">
            Sage Pastel Payroll & Driver Deductions
          </h2>
          <p className="mt-1 text-sm text-slate-600">
            Aggregate active driver warning deductions, verify BCEA Section 34
            compliance, and export batches directly for Sage Pastel Payroll.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Button
            onClick={() => {
              void batches.refetch()
            }}
            type="button"
            variant="outline"
          >
            <RefreshCcw size={16} /> Refresh
          </Button>
          {canManage ? (
            <Button
              onClick={() => {
                setIsCreating(true)
              }}
              type="button"
            >
              <Plus size={16} /> New Payroll Batch
            </Button>
          ) : null}
        </div>
      </div>

      {batches.isLoading ? (
        <div className="rounded-xl border bg-white p-8 text-center text-sm text-slate-500">
          Loading payroll batches…
        </div>
      ) : batches.isError ? (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">
          <p className="font-bold">Failed to load payroll batches</p>
          <p>{batches.error.message}</p>
        </div>
      ) : batches.data?.length === 0 ? (
        <div className="rounded-xl border border-dashed bg-slate-50 p-8 text-center">
          <p className="font-bold text-slate-950">No payroll batches yet</p>
          <p className="mt-1 text-sm text-slate-600">
            Create a batch to aggregate approved driver deductions for the pay
            cycle.
          </p>
          {canManage ? (
            <Button
              className="mt-4"
              onClick={() => {
                setIsCreating(true)
              }}
              type="button"
            >
              <Plus size={16} /> Prepare First Batch
            </Button>
          ) : null}
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y text-sm">
              <thead className="bg-slate-50 text-left text-xs font-bold tracking-wide text-slate-500 uppercase">
                <tr>
                  <th className="px-4 py-3">Batch Number</th>
                  <th className="px-4 py-3">Period</th>
                  <th className="px-4 py-3">Frequency</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3 text-right">Lines</th>
                  <th className="px-4 py-3 text-right">Total Deductions</th>
                  <th className="px-4 py-3">Dates</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {batches.data?.map((batch) => (
                  <tr key={batch.id} className="hover:bg-slate-50/70">
                    <td className="px-4 py-3 font-mono font-bold text-slate-950">
                      <button
                        className="text-left underline hover:text-brand-700"
                        onClick={() => {
                          setSelectedBatchId(batch.id)
                        }}
                        type="button"
                      >
                        {batch.batchNumber}
                      </button>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-slate-700">
                      {batch.periodStartDate} to {batch.periodEndDate}
                    </td>
                    <td className="px-4 py-3">
                      <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-700">
                        {batch.frequency}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <PayrollStatusBadge status={batch.status} />
                    </td>
                    <td className="px-4 py-3 text-right font-medium text-slate-700">
                      {batch.lineCount}
                    </td>
                    <td className="px-4 py-3 text-right font-bold text-red-700">
                      -{money(batch.totalDeductionsAmount, batch.currencyCode)}
                    </td>
                    <td className="px-4 py-3 text-xs text-slate-500">
                      Created {dateTime(batch.createdAtUtc)}
                      {batch.exportedAtUtc ? (
                        <span className="block text-emerald-700">
                          Exported {dateTime(batch.exportedAtUtc)}
                        </span>
                      ) : null}
                    </td>
                    <td className="space-x-2 px-4 py-3 text-right">
                      <Button
                        className="inline-flex min-h-8 items-center gap-1 text-xs font-bold text-emerald-800"
                        disabled={download.isPending}
                        onClick={() => {
                          download.mutate({ batch, format: 'pastel' })
                        }}
                        title="Download Pastel Payroll CSV (Code, TransCode, Amount)"
                        type="button"
                        variant="outline"
                      >
                        <FileSpreadsheet size={13} /> Pastel CSV
                      </Button>
                      <Button
                        className="inline-flex min-h-8 items-center gap-1 text-xs font-bold"
                        disabled={download.isPending}
                        onClick={() => {
                          download.mutate({ batch, format: 'generic' })
                        }}
                        title="Download standard deductions reconciliation CSV"
                        type="button"
                        variant="outline"
                      >
                        <Download size={13} /> CSV
                      </Button>
                      {batch.status === 'Draft' && canManage ? (
                        <Button
                          className="min-h-8 px-3 text-xs"
                          disabled={approve.isPending}
                          onClick={() => {
                            approve.mutate(batch.id)
                          }}
                          type="button"
                        >
                          Approve
                        </Button>
                      ) : null}
                      {batch.status === 'Approved' && canManage ? (
                        <Button
                          className="min-h-8 px-3 text-xs"
                          disabled={markExported.isPending}
                          onClick={() => {
                            markExported.mutate(batch.id)
                          }}
                          type="button"
                          variant="outline"
                        >
                          Mark Exported
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Selected Batch Details Modal */}
      {selectedBatchId && activeBatch ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="max-h-[90vh] w-full max-w-4xl overflow-hidden rounded-xl bg-white shadow-2xl flex flex-col">
            <div className="flex items-center justify-between border-b p-5">
              <div>
                <h3 className="text-xl font-black text-slate-950">
                  Payroll Batch {activeBatch.batchNumber}
                </h3>
                <p className="text-xs text-slate-500">
                  Period: {activeBatch.periodStartDate} to{' '}
                  {activeBatch.periodEndDate} · {activeBatch.frequency}
                </p>
              </div>
              <button
                className="rounded-lg p-1 hover:bg-slate-100"
                onClick={() => {
                  setSelectedBatchId(null)
                }}
                type="button"
              >
                <X size={20} />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-5 space-y-4">
              <div className="grid grid-cols-3 gap-4 rounded-lg bg-slate-50 p-4 text-center">
                <div>
                  <p className="text-xs text-slate-500">Status</p>
                  <p className="font-bold text-slate-900">
                    {activeBatch.status}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-slate-500">Deduction Lines</p>
                  <p className="font-bold text-slate-900">
                    {activeBatch.lines.length}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-slate-500">Total Deducted</p>
                  <p className="font-bold text-red-700">
                    -{money(activeBatch.totalDeductionsAmount, activeBatch.currencyCode)}
                  </p>
                </div>
              </div>

              <div className="rounded-lg border overflow-x-auto">
                <table className="min-w-full divide-y text-sm">
                  <thead className="bg-slate-50 text-left text-xs font-bold text-slate-500 uppercase">
                    <tr>
                      <th className="px-3 py-2">Payroll Code</th>
                      <th className="px-3 py-2">Driver</th>
                      <th className="px-3 py-2">Trans Code</th>
                      <th className="px-3 py-2">Description</th>
                      <th className="px-3 py-2 text-right">Deduction</th>
                      <th className="px-3 py-2 text-right">Remaining Balance</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {activeBatch.lines.map((line) => (
                      <tr key={line.id} className="hover:bg-slate-50/50">
                        <td className="px-3 py-2 font-mono text-xs font-bold text-slate-800">
                          {line.driverEmployeeNumber ?? '—'}
                        </td>
                        <td className="px-3 py-2 font-medium text-slate-900">
                          {line.driverName}
                        </td>
                        <td className="px-3 py-2 font-mono text-xs text-slate-600">
                          {line.transactionCode}
                        </td>
                        <td className="px-3 py-2 text-slate-700">
                          {line.description}
                        </td>
                        <td className="px-3 py-2 text-right font-bold text-red-700">
                          -{money(line.amount, activeBatch.currencyCode)}
                        </td>
                        <td className="px-3 py-2 text-right font-mono text-slate-600">
                          {money(line.balanceAfterDeduction, activeBatch.currencyCode)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="flex justify-end gap-3 border-t p-4">
              <Button
                onClick={() => {
                  setSelectedBatchId(null)
                }}
                type="button"
                variant="outline"
              >
                Close
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      {/* New Payroll Batch Modal */}
      {isCreating ? (
        <CreatePayrollBatchModal
          onClose={() => {
            setIsCreating(false)
          }}
          onCreated={() => {
            setIsCreating(false)
            void queryClient.invalidateQueries({
              queryKey: ['finance', 'payroll-batches'],
            })
          }}
        />
      ) : null}
    </section>
  )
}

function CreatePayrollBatchModal({
  onClose,
  onCreated,
}: {
  onClose: () => void
  onCreated: () => void
}) {
  const auth = useAuth()
  const today = useMemo(() => new Date().toISOString().slice(0, 10), [])
  const monthStart = useMemo(
    () =>
      new Date(new Date().getFullYear(), new Date().getMonth(), 1)
        .toISOString()
        .slice(0, 10),
    [],
  )

  const [frequency, setFrequency] = useState<'Monthly' | 'Weekly' | 'Fortnightly'>('Monthly')
  const [periodStartDate, setPeriodStartDate] = useState(monthStart)
  const [periodEndDate, setPeriodEndDate] = useState(today)
  const [batchNumber, setBatchNumber] = useState(
    `PAY-${String(new Date().getFullYear())}${String(new Date().getMonth() + 1).padStart(2, '0')}-01`,
  )

  const preview = useMutation({
    mutationFn: () =>
      previewPayrollBatch(auth.request, {
        periodStartDate,
        periodEndDate,
        frequency,
      }),
  })

  const create = useMutation({
    mutationFn: () =>
      createPayrollBatch(auth.request, {
        batchNumber,
        periodStartDate,
        periodEndDate,
        frequency,
        currencyCode: 'ZAR',
      }),
    onSuccess: () => {
      onCreated()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="max-h-[90vh] w-full max-w-4xl overflow-hidden rounded-xl bg-white shadow-2xl flex flex-col">
        <div className="flex items-center justify-between border-b p-5">
          <div>
            <h3 className="text-xl font-black text-slate-950">
              Prepare Sage Pastel Payroll Batch
            </h3>
            <p className="text-xs text-slate-500">
              Calculate driver deduction installments for the current pay period.
            </p>
          </div>
          <button
            className="rounded-lg p-1 hover:bg-slate-100"
            onClick={() => {
              onClose()
            }}
            type="button"
          >
            <X size={20} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-5 space-y-6">
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900 flex items-start gap-2">
            <AlertCircle size={16} className="shrink-0 mt-0.5" />
            <div>
              <strong>BCEA Section 34 Compliance Note:</strong> Only approved
              deductions with signed driver consent are included. Total
              deductions must not exceed 25% of the employee’s remuneration.
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-4">
            <div>
              <label className="block text-xs font-bold text-slate-700">
                Batch Number
              </label>
              <input
                className="mt-1 h-10 w-full rounded-md border px-3 text-sm font-mono"
                onChange={(e) => {
                  setBatchNumber(e.target.value)
                }}
                value={batchNumber}
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700">
                Frequency
              </label>
              <select
                className="mt-1 h-10 w-full rounded-md border bg-white px-3 text-sm"
                onChange={(e) => {
                  setFrequency(
                    e.target.value as 'Monthly' | 'Weekly' | 'Fortnightly',
                  )
                }}
                value={frequency}
              >
                <option value="Monthly">Monthly</option>
                <option value="Weekly">Weekly</option>
                <option value="Fortnightly">Fortnightly</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700">
                Period Start
              </label>
              <input
                className="mt-1 h-10 w-full rounded-md border px-3 text-sm"
                onChange={(e) => {
                  setPeriodStartDate(e.target.value)
                }}
                type="date"
                value={periodStartDate}
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-700">
                Period End
              </label>
              <input
                className="mt-1 h-10 w-full rounded-md border px-3 text-sm"
                onChange={(e) => {
                  setPeriodEndDate(e.target.value)
                }}
                type="date"
                value={periodEndDate}
              />
            </div>
          </div>

          <div>
            <Button
              className="gap-2"
              disabled={preview.isPending}
              onClick={() => {
                preview.mutate()
              }}
              type="button"
              variant="outline"
            >
              <RefreshCcw size={14} /> Calculate Eligible Deductions
            </Button>
          </div>

          {preview.isError ? (
            <div className="rounded-md border border-red-200 bg-red-50 p-3 text-xs text-red-800">
              {preview.error.message}
            </div>
          ) : null}

          {preview.data ? (
            <div className="space-y-4">
              <div className="grid grid-cols-3 gap-4 rounded-lg bg-slate-50 p-4 text-center">
                <div>
                  <p className="text-xs text-slate-500">Eligible Drivers</p>
                  <p className="text-lg font-black text-slate-900">
                    {preview.data.eligibleDriversCount}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-slate-500">Active Deductions</p>
                  <p className="text-lg font-black text-slate-900">
                    {preview.data.totalDeductionsCount}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-slate-500">Total To Deduct</p>
                  <p className="text-lg font-black text-red-700">
                    -{money(preview.data.totalDeductionsAmount, preview.data.currencyCode)}
                  </p>
                </div>
              </div>

              {preview.data.lines.length === 0 ? (
                <div className="rounded-lg border border-dashed p-6 text-center text-sm text-slate-500">
                  No active approved deductions with outstanding balances found
                  for this period.
                </div>
              ) : (
                <div className="max-h-64 overflow-y-auto rounded-lg border">
                  <table className="min-w-full divide-y text-xs">
                    <thead className="bg-slate-50 font-bold text-slate-600">
                      <tr>
                        <th className="px-3 py-2 text-left">Payroll Code</th>
                        <th className="px-3 py-2 text-left">Driver</th>
                        <th className="px-3 py-2 text-left">Trans Code</th>
                        <th className="px-3 py-2 text-left">Description</th>
                        <th className="px-3 py-2 text-right">Installment</th>
                        <th className="px-3 py-2 text-right">Remaining Balance</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {preview.data.lines.map((line) => (
                        <tr key={line.deductionId}>
                          <td className="px-3 py-2 font-mono font-bold text-slate-800">
                            {line.driverEmployeeNumber ?? (
                              <span className="text-amber-600 italic">
                                Missing Pastel Code
                              </span>
                            )}
                          </td>
                          <td className="px-3 py-2 font-medium">
                            {line.driverName}
                          </td>
                          <td className="px-3 py-2 font-mono">
                            {line.transactionCode}
                          </td>
                          <td className="px-3 py-2">{line.description}</td>
                          <td className="px-3 py-2 text-right font-bold text-red-700">
                            -{money(line.installmentAmount, preview.data.currencyCode)}
                          </td>
                          <td className="px-3 py-2 text-right font-mono text-slate-600">
                            {money(line.balanceAfterDeduction, preview.data.currencyCode)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          ) : null}

          {create.isError ? (
            <div className="rounded-md border border-red-200 bg-red-50 p-3 text-xs text-red-800">
              {create.error.message}
            </div>
          ) : null}
        </div>

        <div className="flex justify-end gap-3 border-t p-4">
          <Button
            onClick={() => {
              onClose()
            }}
            type="button"
            variant="outline"
          >
            Cancel
          </Button>
          <Button
            disabled={!preview.data || create.isPending}
            onClick={() => {
              create.mutate()
            }}
            type="button"
          >
            {create.isPending ? 'Creating…' : 'Create Batch'}
          </Button>
        </div>
      </div>
    </div>
  )
}

function PayrollStatusBadge({ status }: { status: string }) {
  if (status === 'Approved') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-bold text-blue-800">
        <CheckCircle2 size={12} /> Approved
      </span>
    )
  }
  if (status === 'Exported') {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-emerald-800">
        <CheckCircle2 size={12} /> Exported
      </span>
    )
  }
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-800">
      Draft
    </span>
  )
}
