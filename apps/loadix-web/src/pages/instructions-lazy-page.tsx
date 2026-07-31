import { lazy } from 'react'

const InstructionsPage = lazy(async () => {
  const module = await import('@/features/instructions/instructions-page')
  return { default: module.InstructionsPage }
})

export function InstructionsLazyPage() {
  return <InstructionsPage />
}
