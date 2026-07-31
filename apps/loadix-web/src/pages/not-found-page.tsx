import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  return (
    <section className="mx-auto max-w-xl rounded-xl border bg-white p-8 shadow-sm">
      <p className="text-brand-700 text-sm font-bold tracking-wider uppercase">
        404
      </p>
      <h1 className="mt-2 text-3xl font-black text-slate-950">
        Page not found
      </h1>
      <p className="mt-3 text-slate-600">
        The page you requested does not exist or has moved.
      </p>
      <Button asChild className="mt-6">
        <Link to="/">Return to dashboard</Link>
      </Button>
    </section>
  )
}
