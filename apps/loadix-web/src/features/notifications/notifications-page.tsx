import { useMemo, useState } from 'react'
import { RotateCw, Save } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  listFailedNotifications,
  listNotificationPreferences,
  listNotificationTemplates,
  previewNotificationTemplate,
  retryFailedNotification,
  saveNotificationPreference,
  saveNotificationTemplate,
  type NotificationTemplate,
} from './notification-api'

export function NotificationsPage() {
  const auth = useAuth()
  const client = useQueryClient()
  const preferences = useQuery({
    queryKey: ['notification-preferences'],
    queryFn: () => listNotificationPreferences(auth.request),
  })
  const templates = useQuery({
    queryKey: ['notification-templates'],
    queryFn: () => listNotificationTemplates(auth.request),
    enabled: auth.user?.permissions.includes('Tenant.Manage') ?? false,
  })
  const failed = useQuery({
    queryKey: ['notification-failed'],
    queryFn: () => listFailedNotifications(auth.request),
    enabled: auth.user?.permissions.includes('Audit.View') ?? false,
  })
  const savePreference = useMutation({
    mutationFn: saveNotificationPreference.bind(null, auth.request),
    onSuccess: () =>
      client.invalidateQueries({ queryKey: ['notification-preferences'] }),
  })

  return (
    <section className="space-y-5" aria-labelledby="notifications-title">
      <header>
        <p className="text-brand-700 text-xs font-bold tracking-widest uppercase">
          Operational signals
        </p>
        <h1 className="text-3xl font-black" id="notifications-title">
          Notifications
        </h1>
      </header>

      <section className="rounded-xl border bg-white p-4">
        <h2 className="font-black">Preferences</h2>
        <div className="mt-3 overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b text-xs font-black text-slate-500 uppercase">
                <th className="py-2">Type</th>
                <th>In-app</th>
                <th>Email</th>
                <th>SMS</th>
              </tr>
            </thead>
            <tbody>
              {preferences.data?.map((preference) => (
                <tr className="border-b" key={preference.notificationType}>
                  <td className="py-3 font-semibold">
                    {preference.notificationType}
                  </td>
                  {(
                    ['inAppEnabled', 'emailEnabled', 'smsEnabled'] as const
                  ).map((key) => (
                    <td key={key}>
                      <input
                        aria-label={`${preference.notificationType} ${key}`}
                        checked={preference[key]}
                        onChange={(event) => {
                          savePreference.mutate({
                            ...preference,
                            [key]: event.target.checked,
                          })
                        }}
                        type="checkbox"
                      />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {auth.user?.permissions.includes('Tenant.Manage') ? (
        <TemplateEditor templates={templates.data ?? []} />
      ) : null}

      {auth.user?.permissions.includes('Audit.View') ? (
        <section className="rounded-xl border bg-white p-4">
          <h2 className="font-black">Failed deliveries</h2>
          <div className="mt-3 space-y-2">
            {(failed.data ?? []).map((item) => (
              <div
                className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3 text-sm"
                key={item.deliveryId}
              >
                <div>
                  <p className="font-bold">
                    {item.notificationType} via {item.channel}
                  </p>
                  <p className="text-slate-600">{item.failure}</p>
                </div>
                <Button
                  className="gap-2"
                  onClick={() => {
                    void retryFailedNotification(
                      auth.request,
                      item.deliveryId,
                    ).then(() =>
                      client.invalidateQueries({
                        queryKey: ['notification-failed'],
                      }),
                    )
                  }}
                  type="button"
                  variant="outline"
                >
                  <RotateCw aria-hidden="true" size={16} />
                  Retry
                </Button>
              </div>
            ))}
          </div>
        </section>
      ) : null}
    </section>
  )
}

function TemplateEditor({
  templates,
}: {
  templates: readonly NotificationTemplate[]
}) {
  const auth = useAuth()
  const client = useQueryClient()
  const first = templates[0]
  const [model, setModel] = useState({
    notificationType: first?.notificationType ?? 'TripAssignment',
    channel: first?.channel ?? 'email',
    subjectTemplate: first?.subjectTemplate ?? '{{Title}}',
    bodyTemplate: first?.bodyTemplate ?? '{{Body}}',
  })
  const save = useMutation({
    mutationFn: saveNotificationTemplate.bind(null, auth.request),
    onSuccess: () =>
      client.invalidateQueries({ queryKey: ['notification-templates'] }),
  })
  const preview = useQuery({
    queryKey: ['notification-template-preview', model],
    queryFn: () => previewNotificationTemplate(auth.request, model),
  })
  const channels = useMemo(() => ['in-app', 'email', 'sms'], [])
  return (
    <section className="rounded-xl border bg-white p-4">
      <h2 className="font-black">Template preview</h2>
      <div className="mt-3 grid gap-3 lg:grid-cols-2">
        <div className="space-y-3">
          <input
            className="min-h-11 w-full rounded-md border px-3"
            onChange={(event) => {
              setModel({ ...model, notificationType: event.target.value })
            }}
            value={model.notificationType}
          />
          <select
            className="min-h-11 w-full rounded-md border px-3"
            onChange={(event) => {
              setModel({ ...model, channel: event.target.value })
            }}
            value={model.channel}
          >
            {channels.map((channel) => (
              <option key={channel}>{channel}</option>
            ))}
          </select>
          <input
            className="min-h-11 w-full rounded-md border px-3"
            onChange={(event) => {
              setModel({ ...model, subjectTemplate: event.target.value })
            }}
            value={model.subjectTemplate}
          />
          <textarea
            className="min-h-32 w-full rounded-md border p-3"
            onChange={(event) => {
              setModel({ ...model, bodyTemplate: event.target.value })
            }}
            value={model.bodyTemplate}
          />
          <Button
            className="gap-2"
            onClick={() => {
              save.mutate(model)
            }}
            type="button"
          >
            <Save aria-hidden="true" size={16} />
            Save template
          </Button>
        </div>
        <div className="rounded-md border bg-slate-50 p-4 text-sm">
          <p className="font-black">{preview.data?.subject}</p>
          <p className="mt-2 text-slate-700">{preview.data?.body}</p>
        </div>
      </div>
    </section>
  )
}
