import { LoaderCircle } from 'lucide-react'

export function LoadingPage() {
  return (
    <div
      aria-label="Loading page"
      className="grid min-h-72 place-items-center"
      role="status"
    >
      <LoaderCircle
        aria-hidden="true"
        className="text-brand-600 animate-spin"
        size={28}
      />
    </div>
  )
}
