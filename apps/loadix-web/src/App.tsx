import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'
import { Suspense } from 'react'
import { AppErrorBoundary } from '@/components/app-error-boundary'
import { AuthProvider } from '@/auth/auth-context'
import { router } from '@/router'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
      staleTime: 30_000,
    },
  },
})

export function App() {
  return (
    <AppErrorBoundary>
      <AuthProvider>
        <QueryClientProvider client={queryClient}>
          <Suspense fallback={<p className="p-8">Loading LoadIx…</p>}>
            <RouterProvider router={router} />
          </Suspense>
        </QueryClientProvider>
      </AuthProvider>
    </AppErrorBoundary>
  )
}
