import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Cloud, CloudOff, LoaderCircle } from 'lucide-react'
import { getSystemInfo } from '@/api/system'
import { cn } from '@/lib/utils'

export function ConnectionStatus() {
  const [browserOnline, setBrowserOnline] = useState(navigator.onLine)
  const systemQuery = useQuery({
    queryKey: ['system-info'],
    queryFn: ({ signal }) => getSystemInfo(signal),
    refetchInterval: 30_000,
    retry: false,
  })

  useEffect(() => {
    const updateOnlineState = () => {
      setBrowserOnline(navigator.onLine)
    }
    window.addEventListener('online', updateOnlineState)
    window.addEventListener('offline', updateOnlineState)

    return () => {
      window.removeEventListener('online', updateOnlineState)
      window.removeEventListener('offline', updateOnlineState)
    }
  }, [])

  const isConnected = browserOnline && systemQuery.isSuccess
  const isChecking = browserOnline && systemQuery.isPending
  const label = isChecking
    ? 'Checking connection'
    : isConnected
      ? 'API connected'
      : 'Working offline'
  const Icon = isChecking ? LoaderCircle : isConnected ? Cloud : CloudOff

  return (
    <div
      className={cn(
        'inline-flex min-h-9 items-center gap-2 rounded-full border px-3 text-xs font-semibold',
        isConnected
          ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
          : isChecking
            ? 'border-slate-200 bg-slate-50 text-slate-600'
            : 'border-amber-200 bg-amber-50 text-amber-800',
      )}
      role="status"
    >
      <Icon
        aria-hidden="true"
        className={cn(isChecking && 'animate-spin')}
        size={15}
      />
      <span>{label}</span>
    </div>
  )
}
