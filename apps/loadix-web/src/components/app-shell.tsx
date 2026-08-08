import { useState } from 'react'
import { Link, Outlet } from '@tanstack/react-router'
import {
  ClipboardList,
  Building2,
  MapPin,
  Package,
  Contact,
  LayoutDashboard,
  Menu,
  Receipt,
  Route,
  Truck,
  Users,
  X,
  FileText,
  Smartphone,
  Bell,
  BarChart3,
  Cpu,
} from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { ConnectionStatus } from '@/components/connection-status'
import { Button } from '@/components/ui/button'
import { NotificationCenter } from '@/features/notifications/notification-center'
import { cn } from '@/lib/utils'

const navigationItems = [
  { label: 'Dashboard', to: '/', icon: LayoutDashboard },
  { label: 'Operations', to: '/operations', icon: Route },
  { label: 'Notifications', to: '/notifications', icon: Bell },
  {
    label: 'Finance',
    to: '/finance',
    icon: Receipt,
    permission: 'Finance.View',
  },
  {
    label: 'Reports',
    to: '/reports',
    icon: BarChart3,
    permission: 'Report.View',
  },
  {
    label: 'Trips',
    to: '/trips',
    icon: ClipboardList,
    permission: 'Trip.View',
  },
  {
    label: 'Driver workspace',
    to: '/driver',
    icon: Smartphone,
    permission: 'Trip.Driver.Execute',
  },
  {
    label: 'Load instructions',
    to: '/load-instructions',
    icon: ClipboardList,
    permission: 'Trip.View',
  },
  {
    label: 'Branches',
    to: '/branches',
    icon: Building2,
    permission: 'Tenant.View',
  },
  {
    label: 'Customers',
    to: '/customers',
    icon: Contact,
    permission: 'Customer.View',
  },
  {
    label: 'Locations',
    to: '/locations',
    icon: MapPin,
    permission: 'Customer.View',
  },
  {
    label: 'Commodities',
    to: '/commodities',
    icon: Package,
    permission: 'Contract.View',
  },
  {
    label: 'Contracts',
    to: '/contracts',
    icon: FileText,
    permission: 'Contract.View',
  },
  { label: 'Trucks', to: '/fleet', icon: Truck, permission: 'Fleet.View' },
  { label: 'Drivers', to: '/drivers', icon: Contact, permission: 'Fleet.View' },
  { label: 'Trailers', to: '/trailers', icon: Truck, permission: 'Fleet.View' },
  { label: 'Users', to: '/users', icon: Users, permission: 'User.View' },
  {
    label: 'Edge devices',
    to: '/edge-devices',
    icon: Cpu,
    permission: 'EdgeDevice.View',
  },
] as const

export function AppShell() {
  const [navigationOpen, setNavigationOpen] = useState(false)
  const auth = useAuth()
  const isDemo = auth.user?.tenantId === '10000000-0000-0000-0000-000000000001'

  return (
    <div className="min-h-screen bg-slate-100/80">
      <a
        className="bg-brand-600 fixed top-2 left-2 z-50 -translate-y-20 rounded-md px-4 py-2 font-semibold text-white focus:translate-y-0"
        href="#main-content"
      >
        Skip to content
      </a>

      {navigationOpen ? (
        <button
          aria-label="Close navigation"
          className="fixed inset-0 z-30 bg-slate-950/45 lg:hidden"
          onClick={() => {
            setNavigationOpen(false)
          }}
          type="button"
        />
      ) : null}

      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-slate-800 bg-slate-950 text-slate-200 transition-transform lg:translate-x-0',
          navigationOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-18 items-center justify-between border-b border-slate-800 px-5">
          <Link
            className="flex items-center gap-3"
            onClick={() => {
              setNavigationOpen(false)
            }}
            to="/"
          >
            <span className="bg-brand-500 grid size-9 place-items-center rounded-lg font-black text-slate-950">
              LX
            </span>
            <span>
              <span className="block text-base font-bold tracking-tight text-white">
                LoadIx
              </span>
              <span className="block text-[10px] font-semibold tracking-[0.18em] text-slate-400 uppercase">
                Operations
              </span>
            </span>
          </Link>
          <Button
            aria-label="Close navigation"
            className="text-slate-300 hover:bg-slate-800 hover:text-white lg:hidden"
            onClick={() => {
              setNavigationOpen(false)
            }}
            type="button"
            variant="ghost"
          >
            <X aria-hidden="true" size={20} />
          </Button>
        </div>

        <nav
          aria-label="Primary navigation"
          className="flex-1 space-y-1 overflow-y-auto px-3 py-5"
        >
          {navigationItems
            .filter(
              (item) =>
                !('permission' in item) ||
                auth.user?.permissions.includes(item.permission),
            )
            .map((item) => (
              <Link
                activeProps={{
                  className: 'bg-brand-500/15 text-brand-100 border-brand-500',
                }}
                className="flex min-h-11 items-center gap-3 rounded-md border-l-2 border-transparent px-3 text-sm font-medium text-slate-300 hover:bg-slate-900 hover:text-white"
                key={item.to}
                onClick={() => {
                  setNavigationOpen(false)
                }}
                to={item.to}
              >
                <item.icon aria-hidden="true" size={19} />
                {item.label}
              </Link>
            ))}
        </nav>

        <div className="border-t border-slate-800 p-4 text-xs text-slate-500">
          Pilot demonstration · synthetic data
        </div>
      </aside>

      <div className="lg:pl-64">
        {isDemo ? (
          <div className="bg-amber-50 px-4 py-2 text-center text-xs font-semibold text-amber-900 sm:px-6">
            LoadIx Demo Transport · Synthetic training data · Resettable workspace
          </div>
        ) : null}
        <header className="sticky top-0 z-20 flex h-18 items-center justify-between border-b bg-white/95 px-4 backdrop-blur sm:px-6">
          <div className="flex items-center gap-3">
            <Button
              aria-expanded={navigationOpen}
              aria-label="Open navigation"
              className="lg:hidden"
              onClick={() => {
                setNavigationOpen(true)
              }}
              type="button"
              variant="outline"
            >
              <Menu aria-hidden="true" size={20} />
            </Button>
            <div>
              <p className="text-xs font-semibold tracking-wider text-slate-500 uppercase">
                Live workspace
              </p>
              <p className="font-bold text-slate-950">
                Johannesburg Operations
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <ConnectionStatus />
            <NotificationCenter />
            <details className="relative">
              <summary className="cursor-pointer list-none rounded-lg border px-3 py-2 text-sm font-bold text-slate-800">
                {auth.user?.displayName}
              </summary>
              <div className="absolute right-0 mt-2 w-64 rounded-lg border bg-white p-3 shadow-xl">
                <p className="font-bold text-slate-950">
                  {auth.user?.displayName}
                </p>
                <p className="truncate text-xs text-slate-500">
                  {auth.user?.email}
                </p>
                <Button
                  className="mt-3 w-full"
                  onClick={() => {
                    void auth.logout()
                  }}
                  type="button"
                  variant="outline"
                >
                  Log out
                </Button>
              </div>
            </details>
          </div>
        </header>

        <main className="p-4 sm:p-6 lg:p-8" id="main-content" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
    </div>
  )
}
