import { cn } from '@/lib/utils'

export function StatusBadge({ active }: { active: boolean }) {
  return (
    <span
      className={cn(
        'inline-flex rounded-full px-2.5 py-1 text-xs font-bold',
        active
          ? 'bg-emerald-100 text-emerald-800'
          : 'bg-slate-200 text-slate-700',
      )}
    >
      {active ? 'Active' : 'Inactive'}
    </span>
  )
}
