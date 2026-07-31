import { lazy } from 'react'

const ContractsPage = lazy(() =>
  import('@/features/contracts/contracts-page').then((module) => ({
    default: module.ContractsPage,
  })),
)

export function ContractsLazyPage() {
  return <ContractsPage />
}
