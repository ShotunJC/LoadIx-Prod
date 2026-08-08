import {
  ArrowRight,
  CircleAlert,
  Clock3,
  FileText,
  Route,
  Truck,
} from 'lucide-react'
import { Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useAuth } from '@/auth/auth-context'
import { getDashboard } from '@/features/reporting/reporting-api'

const summaryCards = [
  {
    label: 'Active trips',
    value: '—',
    detail: 'Awaiting operational data',
    icon: Truck,
  },
  {
    label: 'Exceptions',
    value: '—',
    detail: 'No exception data yet',
    icon: CircleAlert,
  },
  {
    label: 'Awaiting POD',
    value: '—',
    detail: 'Document workflow follows',
    icon: Clock3,
  },
] as const

export function DashboardPage() {
  const auth = useAuth()
  const dashboard = useQuery({
    queryKey: ['reports', 'dashboard', 'home'],
    queryFn: () => getDashboard(auth.request),
  })
  const metrics = dashboard.data
    ? [
        {
          label: 'Active trips',
          value: String(dashboard.data.activeTrips),
          detail: 'Current operational workload',
          icon: Truck,
        },
        {
          label: 'Delayed trips',
          value: String(dashboard.data.delayedTrips),
          detail: 'Requires controller attention',
          icon: CircleAlert,
        },
        {
          label: 'Awaiting reconciliation',
          value: String(dashboard.data.awaitingReconciliation),
          detail: 'Pending weight and document review',
          icon: Clock3,
        },
      ]
    : summaryCards

  return (
    <div className="mx-auto max-w-7xl">
      <div className="flex flex-col justify-between gap-3 border-b border-slate-300 pb-5 sm:flex-row sm:items-end">
        <div>
          <p className="text-brand-700 text-sm font-bold tracking-wider uppercase">
            Control tower
          </p>
          <h1 className="mt-1 text-3xl font-black tracking-tight text-slate-950">
            Operations dashboard
          </h1>
          <p className="mt-2 max-w-2xl text-sm text-slate-600">
            Monitor active movements, reconciliation exceptions and finance
            readiness across the demonstration fleet.
          </p>
        </div>
        <p className="text-sm font-medium text-slate-500">
          Africa/Johannesburg · ZAR
        </p>
      </div>

      <section
        aria-label="Operational summary"
        className="mt-6 grid gap-4 md:grid-cols-3"
      >
        {metrics.map((card) => (
          <article
            className="rounded-xl border bg-white p-5 shadow-[0_1px_2px_rgba(15,23,42,0.04)]"
            key={card.label}
          >
            <div className="flex items-start justify-between">
              <div>
                <p className="text-sm font-semibold text-slate-500">
                  {card.label}
                </p>
                <p className="mt-3 text-3xl font-black text-slate-950">
                  {card.value}
                </p>
              </div>
              <span className="grid size-10 place-items-center rounded-lg bg-slate-100 text-slate-600">
                <card.icon aria-hidden="true" size={20} />
              </span>
            </div>
            <p className="mt-4 border-t pt-4 text-xs text-slate-500">
              {card.detail}
            </p>
          </article>
        ))}
      </section>

      {dashboard.isError ? (
        <div
          className="mt-4 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          role="alert"
        >
          Dashboard data could not be loaded. Check the connection and try
          again.
        </div>
      ) : null}

      <div className="mt-5 flex justify-end">
        <Link
          className="text-brand-700 text-sm font-semibold hover:underline"
          to="/reports"
        >
          Open all reports <ArrowRight className="ml-1 inline" size={16} />
        </Link>
      </div>

      <section className="mt-6 overflow-hidden rounded-xl border bg-white shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
        <div className="flex items-center justify-between border-b px-5 py-4">
          <div>
            <h2 className="font-bold text-slate-950">Demo journeys</h2>
            <p className="mt-0.5 text-xs text-slate-500">
              Start with these guided operational workspaces.
            </p>
          </div>
          <ArrowRight aria-hidden="true" className="text-slate-400" size={19} />
        </div>
        <div className="grid gap-3 p-5 sm:grid-cols-3">
          <Link
            className="hover:border-brand-500 hover:bg-brand-50 rounded-lg border p-4"
            to="/operations"
          >
            <Route className="text-brand-700" aria-hidden="true" size={22} />
            <p className="mt-3 font-bold text-slate-900">Track active trips</p>
            <p className="mt-1 text-sm text-slate-600">
              Open the live operations board and exception queue.
            </p>
          </Link>
          <Link
            className="hover:border-brand-500 hover:bg-brand-50 rounded-lg border p-4"
            to="/trips"
          >
            <Truck className="text-brand-700" aria-hidden="true" size={22} />
            <p className="mt-3 font-bold text-slate-900">Run a trip workflow</p>
            <p className="mt-1 text-sm text-slate-600">
              Dispatch, weigh, deliver and reconcile a synthetic trip.
            </p>
          </Link>
          <Link
            className="hover:border-brand-500 hover:bg-brand-50 rounded-lg border p-4"
            to="/finance"
          >
            <FileText className="text-brand-700" aria-hidden="true" size={22} />
            <p className="mt-3 font-bold text-slate-900">
              Prepare an invoice batch
            </p>
            <p className="mt-1 text-sm text-slate-600">
              Use approved invoice-ready trips with transparent costs.
            </p>
          </Link>
        </div>
      </section>
    </div>
  )
}
