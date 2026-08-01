import { useMemo, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Download, RefreshCw } from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { listMasterData } from '@/features/master-data/master-data-api'
import {
  downloadExport,
  enqueueExport,
  exportReport,
  getDashboard,
  getExportJob,
  getReport,
  type ReportExportJob,
} from './reporting-api'

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

const pageSizes = [25, 50, 100] as const

function number(value: number, maximumFractionDigits = 0) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits }).format(
    value,
  )
}

function currency(value: number, code: string) {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: code,
  }).format(value)
}

function percent(value: number) {
  return `${number(value, 2)}%`
}

function display(value: unknown) {
  if (value === null || value === undefined) return 'N/A'
  if (typeof value === 'number') return number(value, 3)
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (typeof value === 'string') return value
  return JSON.stringify(value)
}

function textValue(value: unknown, fallback: string) {
  if (typeof value === 'string' && value) return value
  if (typeof value === 'number' || typeof value === 'boolean')
    return String(value)
  return fallback
}

function downloadBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = name
  anchor.click()
  URL.revokeObjectURL(url)
}

export function ReportingPage() {
  const auth = useAuth()
  const [report, setReport] = useState('trip-register')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [branchSearch, setBranchSearch] = useState('')
  const [customerSearch, setCustomerSearch] = useState('')
  const [branchId, setBranchId] = useState('')
  const [customerId, setCustomerId] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState<number>(50)
  const [jobId, setJobId] = useState<string | null>(null)
  const invalidDateRange = Boolean(from && to && to < from)

  const params = useMemo(() => {
    const value = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    })
    if (from) value.set('from', from)
    if (to) value.set('to', to)
    if (branchId) value.set('branchId', branchId)
    if (customerId) value.set('customerId', customerId)
    return value
  }, [page, pageSize, from, to, branchId, customerId])

  const filterParams = useMemo(() => {
    const value = new URLSearchParams()
    if (from) value.set('from', from)
    if (to) value.set('to', to)
    if (branchId) value.set('branchId', branchId)
    if (customerId) value.set('customerId', customerId)
    return value
  }, [from, to, branchId, customerId])

  const branches = useQuery({
    queryKey: ['report-lookups', 'branches', branchSearch],
    queryFn: () =>
      listMasterData(auth.request, 'branches', {
        page: 1,
        pageSize: 50,
        search: branchSearch,
        status: 'active',
        sort: 'name',
        direction: 'asc',
      }),
  })
  const customers = useQuery({
    queryKey: ['report-lookups', 'customers', customerSearch],
    queryFn: () =>
      listMasterData(auth.request, 'customers', {
        page: 1,
        pageSize: 50,
        search: customerSearch,
        status: 'active',
        sort: 'tradingName',
        direction: 'asc',
      }),
  })
  const dashboard = useQuery({
    queryKey: ['reports', 'dashboard', filterParams.toString()],
    queryFn: () => getDashboard(auth.request, filterParams),
    enabled: !invalidDateRange,
  })
  const rows = useQuery({
    queryKey: ['reports', report, params.toString()],
    queryFn: () => getReport(auth.request, report, params),
    enabled: !invalidDateRange,
  })
  const exportMutation = useMutation({
    mutationFn: () => enqueueExport(auth.request, report, filterParams),
    onSuccess: (job) => {
      setJobId(job.jobId)
    },
  })
  const exportJob = useQuery({
    queryKey: ['report-export-job', jobId],
    queryFn: async () => {
      if (!jobId) throw new Error('An export job identifier is required.')
      return getExportJob(auth.request, jobId)
    },
    enabled: jobId !== null,
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status === 'Completed' || status === 'Failed' ? false : 2000
    },
  })

  const selectedReport =
    reports.find(([value]) => value === report)?.[1] ?? report
  const columns = rows.data?.items[0] ? Object.keys(rows.data.items[0]) : []
  const cards = dashboard.data
    ? [
        [
          'Active trips',
          number(dashboard.data.activeTrips),
          'Current operational status',
        ],
        [
          'Trips completed today',
          number(dashboard.data.completedTripsToday),
          'Tenant-local day',
        ],
        [
          'Tonnes delivered today',
          number(dashboard.data.tonnesDeliveredToday, 3),
          'Tenant-local day',
        ],
        [
          'Trips awaiting reconciliation',
          number(dashboard.data.awaitingReconciliation),
          'Current unresolved work',
        ],
        [
          'Trips outside tolerance',
          number(dashboard.data.tripsOutsideTolerance),
          'Configured contract tolerance',
        ],
        [
          'Delayed trips',
          number(dashboard.data.delayedTrips),
          'Current planned-delivery exceptions',
        ],
        [
          'Fleet utilisation',
          percent(dashboard.data.fleetUtilisationPercent),
          'Active vehicles used in period',
        ],
        [
          'Revenue for selected period',
          currency(dashboard.data.revenue, dashboard.data.currencyCode),
          'Completed financial facts',
        ],
        [
          'Estimated gross margin',
          currency(
            dashboard.data.estimatedGrossMargin,
            dashboard.data.currencyCode,
          ),
          'Amount after recorded costs',
        ],
        [
          'Customer concentration',
          percent(dashboard.data.customerConcentrationPercent),
          'Largest customer revenue share',
        ],
        [
          'Upcoming licence expiries',
          number(dashboard.data.upcomingLicenceExpiries),
          'Next 30 tenant-local days',
        ],
      ]
    : []

  async function downloadCurrentPage() {
    const blob = await exportReport(auth.request, report, params)
    downloadBlob(blob, `${report}.csv`)
  }

  async function downloadQueuedJob(job: ReportExportJob) {
    const blob = await downloadExport(auth.request, job.jobId)
    downloadBlob(blob, `${job.report}.csv`)
  }

  function resetFilters() {
    setFrom('')
    setTo('')
    setBranchSearch('')
    setCustomerSearch('')
    setBranchId('')
    setCustomerId('')
    setPage(1)
  }

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
            Tenant-scoped KPIs and bounded reports. Dates use the tenant
            timezone.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => {
            void dashboard.refetch()
            void rows.refetch()
          }}
        >
          <RefreshCw size={16} className="mr-2" /> Refresh
        </Button>
      </header>

      <div
        className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
        aria-label="Reporting KPIs"
      >
        {cards.length
          ? cards.map(([label, value, detail]) => (
              <article key={label} className="rounded-lg border bg-white p-4">
                <p className="text-xs text-slate-500">{label}</p>
                <p className="mt-1 text-2xl font-bold text-slate-950">
                  {value}
                </p>
                <p className="mt-1 text-xs text-slate-500">{detail}</p>
              </article>
            ))
          : Array.from({ length: 11 }, (_, index) => (
              <article
                key={index}
                className="rounded-lg border bg-white p-4"
                aria-label="Loading KPI"
              >
                <p className="text-xs text-slate-400">Loading KPI...</p>
                <p className="mt-2 h-8 w-20 animate-pulse rounded bg-slate-100" />
              </article>
            ))}
      </div>

      <div className="rounded-lg border bg-white p-4">
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-sm font-medium">
            Report
            <select
              aria-label="Report"
              className="mt-1 block rounded border px-3 py-2"
              value={report}
              onChange={(event) => {
                setPage(1)
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
            Date from
            <input
              aria-label="Date from"
              className="mt-1 block rounded border px-3 py-2"
              type="date"
              value={from}
              onChange={(event) => {
                setPage(1)
                setFrom(event.target.value)
              }}
            />
          </label>
          <label className="text-sm font-medium">
            Date to
            <input
              aria-label="Date to"
              className="mt-1 block rounded border px-3 py-2"
              type="date"
              value={to}
              onChange={(event) => {
                setPage(1)
                setTo(event.target.value)
              }}
            />
          </label>
          <label className="text-sm font-medium">
            Branch
            <input
              aria-label="Search branches"
              className="mt-1 block rounded border px-3 py-2"
              placeholder="Search branches"
              type="search"
              value={branchSearch}
              onChange={(event) => {
                setBranchSearch(event.target.value)
              }}
            />
            <select
              aria-label="Branch"
              className="mt-1 block rounded border px-3 py-2"
              value={branchId}
              onChange={(event) => {
                setPage(1)
                setBranchId(event.target.value)
              }}
            >
              <option value="">All branches</option>
              {branches.data?.items.map((item) => (
                <option key={item.id} value={item.id}>
                  {textValue(item.name, textValue(item.reference, item.id))}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-medium">
            Customer
            <input
              aria-label="Search customers"
              className="mt-1 block rounded border px-3 py-2"
              placeholder="Search customers"
              type="search"
              value={customerSearch}
              onChange={(event) => {
                setCustomerSearch(event.target.value)
              }}
            />
            <select
              aria-label="Customer"
              className="mt-1 block rounded border px-3 py-2"
              value={customerId}
              onChange={(event) => {
                setPage(1)
                setCustomerId(event.target.value)
              }}
            >
              <option value="">All customers</option>
              {customers.data?.items.map((item) => (
                <option key={item.id} value={item.id}>
                  {textValue(
                    item.tradingName,
                    textValue(
                      item.legalName,
                      textValue(item.reference, item.id),
                    ),
                  )}
                </option>
              ))}
            </select>
          </label>
          <Button variant="outline" onClick={resetFilters}>
            Reset filters
          </Button>
          <Button
            variant="outline"
            onClick={() => {
              void downloadCurrentPage()
            }}
            disabled={rows.isPending || invalidDateRange}
          >
            <Download size={16} className="mr-2" /> Download CSV
          </Button>
          <Button
            onClick={() => {
              exportMutation.mutate()
            }}
            disabled={exportMutation.isPending || invalidDateRange}
          >
            Queue full export
          </Button>
        </div>
        {invalidDateRange && (
          <p role="alert" className="mt-3 text-sm text-red-700">
            The end date must be on or after the start date.
          </p>
        )}
        {exportMutation.isError && (
          <p role="alert" className="mt-3 text-sm text-red-700">
            Unable to queue the export.
          </p>
        )}
        {exportJob.data && (
          <ExportStatus
            job={exportJob.data}
            onDownload={() => {
              void downloadQueuedJob(exportJob.data)
            }}
          />
        )}
      </div>

      {dashboard.isError && (
        <p role="alert" className="text-sm text-red-700">
          Unable to load dashboard KPIs.
        </p>
      )}
      <div className="rounded-lg border bg-white p-4">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-bold">{selectedReport}</h2>
          <p className="text-xs text-slate-500" aria-live="polite">
            {rows.data
              ? `Updated ${new Date(rows.data.generatedAtUtc).toLocaleString()} - ${number(rows.data.totalCount)} rows - ${rows.data.timeZone}`
              : 'Loading report...'}
          </p>
        </div>
        {rows.isError ? (
          <p role="alert" className="text-sm text-red-700">
            Unable to load this report.
          </p>
        ) : rows.data?.items.length ? (
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <caption className="sr-only">{selectedReport}</caption>
              <thead>
                <tr>
                  {columns.map((column) => (
                    <th
                      scope="col"
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
                  <tr
                    key={`${textValue(item.tripId ?? item.group ?? item.status, String(index))}-${String(index)}`}
                    className="border-b last:border-0"
                  >
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
        ) : rows.isPending ? (
          <p className="py-8 text-center text-sm text-slate-500">
            Loading report...
          </p>
        ) : (
          <p role="status" className="py-8 text-center text-sm text-slate-500">
            No data for the selected filters.
          </p>
        )}
        {rows.data && (
          <nav
            className="mt-4 flex flex-wrap items-center justify-between gap-3"
            aria-label="Report pagination"
          >
            <p className="text-sm text-slate-600">
              Page {rows.data.page} of {rows.data.totalPages || 1} -{' '}
              {number(rows.data.totalCount)} matching rows
            </p>
            <div className="flex items-center gap-2">
              <label className="text-sm">
                Rows per page{' '}
                <select
                  aria-label="Rows per page"
                  className="ml-1 rounded border px-2 py-1"
                  value={pageSize}
                  onChange={(event) => {
                    setPage(1)
                    setPageSize(Number(event.target.value))
                  }}
                >
                  {pageSizes.map((size) => (
                    <option key={size} value={size}>
                      {size}
                    </option>
                  ))}
                </select>
              </label>
              <Button
                variant="outline"
                disabled={page <= 1}
                onClick={() => {
                  setPage((value) => Math.max(value - 1, 1))
                }}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                disabled={page >= rows.data.totalPages}
                onClick={() => {
                  setPage((value) => value + 1)
                }}
              >
                Next
              </Button>
            </div>
          </nav>
        )}
      </div>
    </section>
  )
}

function ExportStatus({
  job,
  onDownload,
}: {
  job: ReportExportJob
  onDownload: () => void
}) {
  return (
    <p className="mt-3 text-sm text-slate-700" role="status" aria-live="polite">
      Export status: {job.status}
      {job.rowCount !== null ? ` - ${number(job.rowCount)} rows` : ''}
      {job.status === 'Completed' && job.downloadUrl ? (
        <Button className="ml-2" variant="outline" onClick={onDownload}>
          Download completed export
        </Button>
      ) : null}
      {job.status === 'Failed' && job.error ? ` - ${job.error}` : ''}
    </p>
  )
}
