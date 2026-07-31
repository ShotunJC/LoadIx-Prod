import { useEffect } from 'react'
import {
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr'
import { Bell, CheckCheck } from 'lucide-react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from './notification-api'

export function NotificationCenter() {
  const auth = useAuth()
  const client = useQueryClient()
  const getAccessToken = auth.getAccessToken
  const inbox = useQuery({
    queryKey: ['notifications'],
    queryFn: () => listNotifications(auth.request),
    refetchInterval: 60_000,
  })

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(new URL('/hubs/operations', window.location.origin).toString(), {
        accessTokenFactory: () => getAccessToken() ?? '',
        transport: import.meta.env.DEV
          ? HttpTransportType.LongPolling
          : HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connection.on('notifications.event', () => {
      globalThis.dispatchEvent(new Event('loadix:notification'))
    })
    // The hub connection joins both the user and tenant groups. Register the
    // operational event channel here even though this component only consumes
    // notifications, otherwise SignalR reports expected tenant broadcasts as
    // unknown client methods.
    connection.on('operations.event', () => undefined)
    let disposed = false
    const start = connection.start().catch(() => undefined)
    void start.then(() => {
      if (disposed) void connection.stop()
    })
    return () => {
      disposed = true
      if (connection.state !== HubConnectionState.Connecting) {
        void connection.stop()
      }
    }
  }, [getAccessToken])

  useEffect(() => {
    const onNotification = () => {
      void client.invalidateQueries({ queryKey: ['notifications'] })
    }
    globalThis.addEventListener('loadix:notification', onNotification)
    return () => {
      globalThis.removeEventListener('loadix:notification', onNotification)
    }
  }, [client])

  const unreadCount = inbox.data?.unreadCount ?? 0
  return (
    <details className="relative">
      <summary
        aria-label={`Notifications, ${String(unreadCount)} unread`}
        className="relative grid size-10 cursor-pointer list-none place-items-center rounded-lg border text-slate-800"
      >
        <Bell aria-hidden="true" size={18} />
        {unreadCount > 0 ? (
          <span className="bg-brand-600 absolute -top-1 -right-1 grid min-w-5 place-items-center rounded-full px-1 text-[11px] font-black text-white">
            {unreadCount}
          </span>
        ) : null}
      </summary>
      <div className="absolute right-0 mt-2 w-96 max-w-[calc(100vw-2rem)] rounded-lg border bg-white p-3 shadow-xl">
        <div className="mb-3 flex items-center justify-between gap-3">
          <p className="font-black text-slate-950">Notifications</p>
          <Button
            className="h-9 gap-2"
            disabled={unreadCount === 0}
            onClick={() => {
              void markAllNotificationsRead(auth.request).then(() =>
                client.invalidateQueries({ queryKey: ['notifications'] }),
              )
            }}
            type="button"
            variant="outline"
          >
            <CheckCheck aria-hidden="true" size={16} />
            Mark all
          </Button>
        </div>
        <div className="max-h-96 space-y-2 overflow-y-auto">
          {inbox.data?.items.length ? (
            inbox.data.items.map((item) => (
              <button
                className="w-full rounded-md border p-3 text-left hover:bg-slate-50"
                key={item.id}
                onClick={() => {
                  void markNotificationRead(auth.request, item.id).then(() =>
                    client.invalidateQueries({ queryKey: ['notifications'] }),
                  )
                }}
                type="button"
              >
                <span className="flex items-center justify-between gap-3">
                  <span className="font-bold text-slate-950">{item.title}</span>
                  {!item.readAtUtc ? (
                    <span className="bg-brand-100 text-brand-800 rounded-full px-2 py-0.5 text-xs font-black">
                      New
                    </span>
                  ) : null}
                </span>
                <span className="mt-1 block text-sm text-slate-600">
                  {item.body}
                </span>
              </button>
            ))
          ) : (
            <p className="rounded-md border border-dashed p-4 text-sm text-slate-600">
              Nothing needs your attention.
            </p>
          )}
        </div>
      </div>
    </details>
  )
}
