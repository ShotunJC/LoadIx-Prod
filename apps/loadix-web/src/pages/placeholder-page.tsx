interface PlaceholderPageProps {
  title: string
}

export function PlaceholderPage({ title }: PlaceholderPageProps) {
  return (
    <section className="mx-auto max-w-7xl rounded-xl border bg-white p-8 shadow-sm">
      <p className="text-brand-700 text-sm font-bold tracking-wider uppercase">
        Foundation
      </p>
      <h1 className="mt-2 text-3xl font-black tracking-tight text-slate-950">
        {title}
      </h1>
      <p className="mt-3 max-w-xl text-slate-600">
        This module is part of the planned LoadIx delivery sequence and has not
        been implemented in the repository foundation milestone.
      </p>
    </section>
  )
}
