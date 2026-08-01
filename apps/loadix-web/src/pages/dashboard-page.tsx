import { ArrowRight, CircleAlert, Clock3, Truck } from 'lucide-react'
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
            The LoadIx foundation is running. Operational metrics will appear as
            trip workflows are delivered.
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
            <h2 className="font-bold text-slate-950">Recent movement</h2>
            <p className="mt-0.5 text-xs text-slate-500">
              Active trip events will be shown here.
            </p>
          </div>
          <ArrowRight aria-hidden="true" className="text-slate-400" size={19} />
        </div>
        <div className="grid min-h-48 place-items-center p-8 text-center">
          <div>
            <span className="mx-auto grid size-12 place-items-center rounded-full bg-slate-100 text-slate-500">
              <Truck aria-hidden="true" size={22} />
            </span>
            <p className="mt-4 font-semibold text-slate-800">
              No trip activity yet
            </p>
            <p className="mt-1 text-sm text-slate-500">
              This workspace is ready for the next delivery milestone.
            </p>
          </div>
        </div>
      </section>
    </div>
  )
}
