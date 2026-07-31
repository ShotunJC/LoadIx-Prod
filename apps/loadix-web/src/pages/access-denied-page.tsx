import { Link } from '@tanstack/react-router'

export function AccessDeniedPage() {
  return (
    <main className="grid min-h-screen place-items-center bg-slate-100 p-6">
      <section className="max-w-lg rounded-xl bg-white p-8 text-center shadow">
        <p className="text-sm font-bold tracking-widest text-amber-700 uppercase">
          Access denied
        </p>
        <h1 className="mt-3 text-3xl font-black">You do not have permission</h1>
        <p className="mt-3 text-slate-600">
          Ask a tenant administrator if your responsibilities require this area.
        </p>
        <Link
          className="bg-brand-600 mt-6 inline-flex rounded-lg px-4 py-2 font-bold text-white"
          to="/"
        >
          Return to dashboard
        </Link>
      </section>
    </main>
  )
}
