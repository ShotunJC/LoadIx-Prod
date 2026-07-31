import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Calculator, PlusCircle } from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  addTripCost,
  approveTripFinancials,
  getTripFinancials,
  recalculateTripFinancials,
  type TripFinancials,
} from '@/features/finance/finance-api'

const costTypes = [
  'DriverCost',
  'FuelEstimate',
  'FuelActual',
  'Toll',
  'Subcontractor',
  'WeighbridgeFee',
  'LoadingFee',
  'UnloadingFee',
  'MiscellaneousApprovedCost',
] as const

function money(value: number, currencyCode: string) {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currencyCode,
  }).format(value)
}

export function TripFinancialPanel({ tripId }: { tripId: string }) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const canManage = auth.user?.permissions.includes('Finance.Manage') ?? false
  const query = useQuery({
    queryKey: ['finance', 'trip', tripId],
    queryFn: () => getTripFinancials(auth.request, tripId),
    retry: false,
  })
  const recalc = useMutation({
    mutationFn: () => recalculateTripFinancials(auth.request, tripId),
    onSuccess: (data) => {
      queryClient.setQueryData(['finance', 'trip', tripId], data)
      void queryClient.invalidateQueries({ queryKey: ['finance'] })
    },
  })
  const approve = useMutation({
    mutationFn: (financials: TripFinancials) =>
      approveTripFinancials(auth.request, tripId, {
        version: financials.version,
        reason: 'Finance review completed',
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(['finance', 'trip', tripId], data)
      void queryClient.invalidateQueries({ queryKey: ['finance'] })
    },
  })

  return (
    <section
      aria-labelledby="trip-financial-title"
      className="rounded-xl border bg-white p-5 shadow-sm"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3
            className="flex items-center gap-2 text-lg font-black text-slate-950"
            id="trip-financial-title"
          >
            <Calculator size={20} /> Financials
          </h3>
          <p className="text-sm text-slate-600">
            Revenue is calculated from the trip’s immutable contract-rate
            snapshot and reviewed tenant tax configuration.
          </p>
        </div>
        <Button
          disabled={!canManage || recalc.isPending}
          onClick={() => {
            recalc.mutate()
          }}
          type="button"
          variant="outline"
        >
          Recalculate
        </Button>
      </div>

      {query.isPending ? (
        <p className="mt-4 text-sm text-slate-600">
          Loading financial details…
        </p>
      ) : query.isError ? (
        <p className="mt-4 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">
          {query.error.message}
        </p>
      ) : (
        <>
          <FinancialSummary financials={query.data} />
          <CostEntryForm
            canManage={canManage}
            financials={query.data}
            onSaved={(updated) => {
              queryClient.setQueryData(['finance', 'trip', tripId], updated)
              void queryClient.invalidateQueries({ queryKey: ['finance'] })
            }}
          />
          <CostEntries financials={query.data} />
          <div className="mt-4 flex flex-wrap items-center gap-2">
            <Button
              disabled={
                !canManage ||
                approve.isPending ||
                query.data.approvalStatus === 'Approved'
              }
              onClick={() => {
                approve.mutate(query.data)
              }}
              type="button"
            >
              Approve financials
            </Button>
            <span className="text-sm text-slate-600">
              Status: <strong>{query.data.approvalStatus}</strong>
              {query.data.invoiceBatchId ? ' · invoiced' : ''}
            </span>
          </div>
        </>
      )}

      {recalc.isError || approve.isError ? (
        <p className="mt-3 rounded-lg bg-red-50 p-3 text-sm text-red-800">
          {recalc.error?.message ?? approve.error?.message}
        </p>
      ) : null}
    </section>
  )
}

function FinancialSummary({ financials }: { financials: TripFinancials }) {
  return (
    <dl className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <Metric
        label="Expected revenue"
        value={money(
          financials.profitability.expectedRevenue.amount,
          financials.currencyCode,
        )}
      />
      <Metric
        label="Costs"
        value={money(
          financials.profitability.totalCost.amount,
          financials.currencyCode,
        )}
      />
      <Metric
        label="Gross margin"
        value={money(
          financials.profitability.grossMargin.amount,
          financials.currencyCode,
        )}
      />
      <Metric
        label="Margin %"
        value={`${financials.profitability.marginPercentage.toFixed(2)}%`}
      />
      <Metric
        label="Billable tonnes"
        value={String(financials.billableTonnes)}
      />
      <Metric
        label="Tax excl."
        value={money(financials.taxExclusiveAmount, financials.currencyCode)}
      />
      <Metric
        label={`Tax ${financials.taxTreatment}`}
        value={money(financials.taxAmount, financials.currencyCode)}
      />
      <Metric
        label="Invoice total"
        value={money(financials.total, financials.currencyCode)}
      />
    </dl>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-slate-50 p-3">
      <dt className="text-xs font-semibold text-slate-500">{label}</dt>
      <dd className="mt-1 font-black text-slate-950">{value}</dd>
    </div>
  )
}

function CostEntryForm({
  canManage,
  financials,
  onSaved,
}: {
  canManage: boolean
  financials: TripFinancials
  onSaved: (financials: TripFinancials) => void
}) {
  const auth = useAuth()
  const [type, setType] = useState<(typeof costTypes)[number]>('FuelEstimate')
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')
  const [isAdjustment, setIsAdjustment] = useState(false)
  const mutation = useMutation({
    mutationFn: () =>
      addTripCost(auth.request, financials.tripId, {
        type,
        amount: Number(amount),
        description,
        isAdjustment,
        version: financials.version,
      }),
    onSuccess: (updated) => {
      setAmount('')
      setDescription('')
      setIsAdjustment(false)
      onSaved(updated)
    },
  })

  return (
    <form
      className="mt-5 grid gap-3 rounded-lg border bg-slate-50 p-4 lg:grid-cols-[1fr_8rem_1.5fr_auto]"
      onSubmit={(event) => {
        event.preventDefault()
        mutation.mutate()
      }}
    >
      <label className="text-sm font-semibold text-slate-700">
        Cost type
        <select
          className="mt-1 block min-h-11 w-full rounded-md border bg-white px-3 font-normal"
          disabled={!canManage}
          onChange={(event) => {
            setType(event.target.value as (typeof costTypes)[number])
          }}
          value={type}
        >
          {costTypes.map((item) => (
            <option key={item} value={item}>
              {item}
            </option>
          ))}
        </select>
      </label>
      <label className="text-sm font-semibold text-slate-700">
        Amount
        <input
          className="mt-1 block min-h-11 w-full rounded-md border px-3 font-normal"
          disabled={!canManage}
          min="0"
          onChange={(event) => {
            setAmount(event.target.value)
          }}
          step="0.0001"
          type="number"
          value={amount}
        />
      </label>
      <label className="text-sm font-semibold text-slate-700">
        Description
        <input
          className="mt-1 block min-h-11 w-full rounded-md border px-3 font-normal"
          disabled={!canManage}
          onChange={(event) => {
            setDescription(event.target.value)
          }}
          placeholder="Fuel slip, toll, subcontractor invoice…"
          value={description}
        />
      </label>
      <div className="flex flex-col justify-end gap-2">
        <label className="flex items-center gap-2 text-sm font-semibold text-slate-700">
          <input
            checked={isAdjustment}
            disabled={!canManage}
            onChange={(event) => {
              setIsAdjustment(event.target.checked)
            }}
            type="checkbox"
          />
          Adjustment
        </label>
        <Button
          disabled={!canManage || mutation.isPending || !amount || !description}
          type="submit"
        >
          <PlusCircle size={16} /> Add cost
        </Button>
      </div>
      {mutation.isError ? (
        <p className="rounded-lg bg-red-50 p-3 text-sm text-red-800 lg:col-span-4">
          {mutation.error.message}
        </p>
      ) : null}
    </form>
  )
}

function CostEntries({ financials }: { financials: TripFinancials }) {
  return (
    <div className="mt-4 overflow-x-auto rounded-lg border">
      <table className="min-w-full divide-y text-sm">
        <thead className="bg-slate-50 text-left text-xs font-bold tracking-wide text-slate-500 uppercase">
          <tr>
            <th className="px-3 py-2">Cost</th>
            <th className="px-3 py-2">Description</th>
            <th className="px-3 py-2 text-right">Amount</th>
          </tr>
        </thead>
        <tbody className="divide-y bg-white">
          {financials.costs.length === 0 ? (
            <tr>
              <td className="px-3 py-4 text-slate-500" colSpan={3}>
                No costs recorded yet.
              </td>
            </tr>
          ) : (
            financials.costs.map((cost) => (
              <tr key={cost.id}>
                <td className="px-3 py-2 font-bold">{cost.type}</td>
                <td className="px-3 py-2">
                  {cost.description}
                  {cost.isAdjustment ? (
                    <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-900">
                      adjustment
                    </span>
                  ) : null}
                </td>
                <td className="px-3 py-2 text-right font-bold">
                  {money(cost.amount, cost.currencyCode)}
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
