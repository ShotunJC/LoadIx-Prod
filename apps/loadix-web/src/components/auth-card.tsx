import type { ReactNode } from 'react'
import { Link } from '@tanstack/react-router'

export function AuthCard({
  title,
  description,
  children,
}: {
  title: string
  description: string
  children: ReactNode
}) {
  return (
    <main className="grid min-h-screen place-items-center bg-slate-950 px-4 py-10">
      <section className="w-full max-w-md rounded-2xl border border-slate-700 bg-white p-7 shadow-2xl">
        <Link className="mb-8 inline-flex items-center gap-3" to="/login">
          <span className="bg-brand-500 grid size-10 place-items-center rounded-lg font-black text-slate-950">
            LX
          </span>
          <span className="text-xl font-black text-slate-950">LoadIx</span>
        </Link>
        <h1 className="text-2xl font-black tracking-tight text-slate-950">
          {title}
        </h1>
        <p className="mt-2 text-sm leading-6 text-slate-600">{description}</p>
        <div className="mt-7">{children}</div>
      </section>
    </main>
  )
}
