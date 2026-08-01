import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
} from '@tanstack/react-router'
import { z } from 'zod'
import { ProtectedShell } from '@/auth/protected-shell'
import { AccessDeniedPage } from '@/pages/access-denied-page'
import { AcceptInvitationPage } from '@/pages/accept-invitation-page'
import { DashboardPage } from '@/pages/dashboard-page'
import { ForgotPasswordPage } from '@/pages/forgot-password-page'
import { LoadingPage } from '@/pages/loading-page'
import { LoginPage } from '@/pages/login-page'
import { NotFoundPage } from '@/pages/not-found-page'
import { OperationsPage } from '@/features/operations/operations-page'
import { NotificationsPage } from '@/features/notifications/notifications-page'
import { ResetPasswordPage } from '@/pages/reset-password-page'
import { UserManagementPage } from '@/pages/user-management-page'
import { MasterDataPage } from '@/features/master-data/master-data-page'
import { ContractsLazyPage } from '@/pages/contracts-lazy-page'
import { InstructionsLazyPage } from '@/pages/instructions-lazy-page'
import { TripsLazyPage } from '@/pages/trips-lazy-page'
import { DriverPage } from '@/features/driver/driver-page'
import { FinancePage } from '@/features/finance/finance-page'
import { ReportingPage } from '@/features/reporting/reporting-page'
import { EdgeDevicesPage } from '@/features/edge/edge-devices-page'

const rootRoute = createRootRoute({
  component: Outlet,
  notFoundComponent: NotFoundPage,
})

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/login',
  validateSearch: z.object({ redirect: z.string().optional() }),
  component: LoginPage,
})

const forgotPasswordRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/forgot-password',
  component: ForgotPasswordPage,
})

const resetPasswordRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/reset-password',
  validateSearch: z.object({
    email: z.string().default(''),
    token: z.string().default(''),
  }),
  component: ResetPasswordPage,
})

const acceptInvitationRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/accept-invitation',
  validateSearch: z.object({ token: z.string().default('') }),
  component: AcceptInvitationPage,
})

const accessDeniedRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/access-denied',
  component: AccessDeniedPage,
})

const protectedRoute = createRoute({
  getParentRoute: () => rootRoute,
  id: '_authenticated',
  component: ProtectedShell,
})

const dashboardRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/',
  component: DashboardPage,
})

const operationsRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/operations',
  component: OperationsPage,
})

const notificationsRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/notifications',
  component: NotificationsPage,
})

const financeRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/finance',
  component: FinancePage,
})

const reportsRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/reports',
  component: ReportingPage,
})

const tripsRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/trips',
  component: TripsLazyPage,
})

const driverRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/driver',
  component: DriverPage,
})

const instructionsRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/load-instructions',
  component: InstructionsLazyPage,
})

const fleetRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/fleet',
  component: () => <MasterDataPage module="vehicles" />,
})

const branchesRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/branches',
  component: () => <MasterDataPage module="branches" />,
})

const customersRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/customers',
  component: () => <MasterDataPage module="customers" />,
})

const locationsRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/locations',
  component: () => <MasterDataPage module="locations" />,
})

const commoditiesRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/commodities',
  component: () => <MasterDataPage module="commodities" />,
})

const contractsRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/contracts',
  component: ContractsLazyPage,
})

const driversRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/drivers',
  component: () => <MasterDataPage module="drivers" />,
})

const trailersRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/trailers',
  component: () => <MasterDataPage module="trailers" />,
})

const usersRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/users',
  component: UserManagementPage,
})

const edgeDevicesRoute = createRoute({
  getParentRoute: () => protectedRoute,
  path: '/edge-devices',
  component: EdgeDevicesPage,
})

const routeTree = rootRoute.addChildren([
  loginRoute,
  forgotPasswordRoute,
  resetPasswordRoute,
  acceptInvitationRoute,
  accessDeniedRoute,
  protectedRoute.addChildren([
    dashboardRoute,
    operationsRoute,
    notificationsRoute,
    financeRoute,
    reportsRoute,
    tripsRoute,
    driverRoute,
    instructionsRoute,
    fleetRoute,
    branchesRoute,
    customersRoute,
    locationsRoute,
    commoditiesRoute,
    contractsRoute,
    driversRoute,
    trailersRoute,
    usersRoute,
    edgeDevicesRoute,
  ]),
])

export const router = createRouter({
  routeTree,
  defaultPendingComponent: LoadingPage,
  defaultPreload: 'intent',
})

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
