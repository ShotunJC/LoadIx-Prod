import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Download, RefreshCw } from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { getDashboard, getReport, exportReport } from './reporting-api'

const reports = [
  ['trip-register', 'Trip register'],
  ['tonnage-by-customer', 'Tonnage by customer'],
  ['tonnage-by-commodity', 'Tonnage by commodity'],
  ['tonnage-by-route', 'Tonnage by route'],
  ['variance', 'Source versus destination variance'],
  ['driver-performance', 'Driver performance'],
  ['vehicle-utilisation', 'Vehicle utilisation'],
  ['contract-performance', 'Contract performance'],
  ['customer-revenue', 'Customer revenue'],
  ['trip-profitability', 'Trip profitability'],
  ['exceptions', 'Exception report'],
  ['outstanding-pod', 'Outstanding proof of delivery'],
  ['expiries', 'Expiry report'],
  ['invoice-status', 'Invoice status'],
] as const

function number(value: number) {
  return new Intl.NumberFormat().format(value)
}
function display(value: unknown) {
  if (value === null || value === undefined) return '—'
  if (typeof value === 'string') return value
  if (typeof value === 'number') return value.toLocaleString()
  if (typeof value === 'boolean') return value ? 'true' : 'false'
  return JSON.stringify(value)
}

export function ReportingPage() {
  const auth = useAuth()
  const [report, setReport] = useState('trip-register')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const params = new URLSearchParams({ page: '1', pageSize: '50' })
  if (from) params.set('fromUtc', new Date(`${from}T00:00:00Z`).toISOString())
  if (to) params.set('toUtc', new Date(`${to}T23:59:59Z`).toISOString())
  const dashboard = useQuery({
    queryKey: ['reports', 'dashboard', from, to],
    queryFn: () => getDashboard(auth.request, params),
  })
  const rows = useQuery({
    queryKey: ['reports', report, from, to],
    queryFn: () => getReport(auth.request, report, params),
  })

  async function download() {
    const blob = await exportReport(auth.request, report, params)
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `${report}.csv`
    anchor.click()
    URL.revokeObjectURL(url)
  }

  const kpis = dashboard.data
    ? [
        ['Active trips', number(dashboard.data.activeTrips)],
        ['Completed today', number(dashboard.data.completedTripsToday)],
        ['Tonnes delivered', number(dashboard.data.tonnesDeliveredToday)],
        [
          'Awaiting reconciliation',
          number(dashboard.data.awaitingReconciliation),
        ],
        ['Delayed trips', number(dashboard.data.delayedTrips)],
        [
          'Estimated gross margin',
          dashboard.data.estimatedGrossMargin.toFixed(2),
        ],
      ]
    : []
  const columns = rows.data?.items[0] ? Object.keys(rows.data.items[0]) : []
  return (
    <section className="space-y-6" aria-labelledby="reports-title">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-brand-700 text-xs font-bold tracking-[0.16em] uppercase">
            Decision support
          </p>
          <h1
            id="reports-title"
            className="mt-1 text-3xl font-black tracking-tight text-slate-950"
          >
            Operations reporting
          </h1>
          <p className="mt-1 text-sm text-slate-600">
            Tenant-scoped KPIs and bounded reports. All values are generated in
            the tenant time zone.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => {
            void dashboard.refetch()
            void rows.refetch()
          }}
        >
          <RefreshCw size={16} className="mr-2" />
          Refresh
        </Button>
      </header>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        {kpis.map(([label, value]) => (
          <article key={label} className="rounded-lg border bg-white p-4">
            <p className="text-xs text-slate-500">{label}</p>
            <p className="mt-1 text-2xl font-bold text-slate-950">{value}</p>
          </article>
        ))}
      </div>
      <div className="rounded-lg border bg-white p-4">
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-sm font-medium">
            Report
            <select
              className="mt-1 block rounded border px-3 py-2"
              value={report}
              onChange={(event) => {
                setReport(event.target.value)
              }}
            >
              {reports.map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-medium">
            From
            <input
              className="mt-1 block rounded border px-3 py-2"
              type="date"
              value={from}
              onChange={(event) => {
                setFrom(event.target.value)
              }}
            />
          </label>
          <label className="text-sm font-medium">
            To
            <input
              className="mt-1 block rounded border px-3 py-2"
              type="date"
              value={to}
              onChange={(event) => {
                setTo(event.target.value)
              }}
            />
          </label>
          <Button
            variant="outline"
            onClick={() => {
              void download()
            }}
            disabled={rows.isLoading}
          >
            <Download size={16} className="mr-2" />
            CSV export
          </Button>
        </div>
      </div>
      <div className="rounded-lg border bg-white p-4">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-bold">
            {reports.find(([value]) => value === report)?.[1]}
          </h2>
          <p className="text-xs text-slate-500">
            {rows.data
              ? `Updated ${new Date(rows.data.generatedAtUtc).toLocaleString()} · ${String(rows.data.totalCount)} rows`
              : 'Loading…'}
          </p>
        </div>
        {rows.isError ? (
          <p role="alert" className="text-sm text-red-700">
            Unable to load this report.
          </p>
        ) : rows.data?.items.length ? (
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <caption className="sr-only">
                {reports.find(([value]) => value === report)?.[1]}
              </caption>
              <thead>
                <tr>
                  {columns.map((column) => (
                    <th
                      className="border-b px-3 py-2 font-semibold"
                      key={column}
                    >
                      {column}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.data.items.map((item, index) => (
                  <tr key={index} className="border-b last:border-0">
                    {columns.map((column) => (
                      <td className="px-3 py-2" key={column}>
                        {display(item[column])}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="py-8 text-center text-sm text-slate-500">
            No data for the selected filters.
          </p>
        )}
      </div>
    </section>
  )
}
