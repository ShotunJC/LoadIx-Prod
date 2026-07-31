import { lazy, Suspense } from 'react'
import { LoadingPage } from '@/pages/loading-page'

const TripsPage = lazy(() =>
  import('@/features/trips/trips-page').then((module) => ({
    default: module.TripsPage,
  })),
)

export function TripsLazyPage() {
  return (
    <Suspense fallback={<LoadingPage />}>
      <TripsPage />
    </Suspense>
  )
}
