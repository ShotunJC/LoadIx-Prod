import { z } from 'zod'

type Requester = (
  input: RequestInfo | URL,
  init?: RequestInit,
) => Promise<Response>

const notificationSchema = z.object({
  id: z.string(),
  type: z.string(),
  title: z.string(),
  body: z.string(),
  createdAtUtc: z.string(),
  readAtUtc: z.string().nullable(),
})

const inboxSchema = z.object({
  items: z.array(notificationSchema),
  unreadCount: z.number(),
})

const preferenceSchema = z.object({
  notificationType: z.string(),
  inAppEnabled: z.boolean(),
  emailEnabled: z.boolean(),
  smsEnabled: z.boolean(),
})

const templateSchema = z.object({
  id: z.string(),
  notificationType: z.string(),
  channel: z.string(),
  subjectTemplate: z.string(),
  bodyTemplate: z.string(),
  modifiedAtUtc: z.string(),
})

const failedSchema = z.object({
  deliveryId: z.string(),
  notificationType: z.string(),
  channel: z.string(),
  attempts: z.number(),
  providerCorrelationId: z.string().nullable(),
  failure: z.string().nullable(),
  nextAttemptAtUtc: z.string(),
})

const previewSchema = z.object({ subject: z.string(), body: z.string() })

export type NotificationInbox = z.infer<typeof inboxSchema>
export type NotificationItem = z.infer<typeof notificationSchema>
export type NotificationPreference = z.infer<typeof preferenceSchema>
export type NotificationTemplate = z.infer<typeof templateSchema>
export type FailedNotification = z.infer<typeof failedSchema>

async function parse<T>(response: Response, schema: z.ZodType<T>): Promise<T> {
  if (!response.ok) throw new Error('The notification request failed.')
  return schema.parse(await response.json())
}

export async function listNotifications(request: Requester) {
  return parse(await request('/api/v1/notifications'), inboxSchema)
}

export async function markNotificationRead(request: Requester, id: string) {
  const response = await request(`/api/v1/notifications/${id}/read`, {
    method: 'POST',
  })
  if (!response.ok) throw new Error('Unable to mark notification read.')
}

export async function markAllNotificationsRead(request: Requester) {
  const response = await request('/api/v1/notifications/read-all', {
    method: 'POST',
  })
  if (!response.ok) throw new Error('Unable to mark notifications read.')
}

export async function listNotificationPreferences(request: Requester) {
  return parse(
    await request('/api/v1/notifications/preferences'),
    z.array(preferenceSchema),
  )
}

export async function saveNotificationPreference(
  request: Requester,
  preference: NotificationPreference,
) {
  const response = await request('/api/v1/notifications/preferences', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(preference),
  })
  if (!response.ok) throw new Error('Unable to save notification preference.')
}

export async function listNotificationTemplates(request: Requester) {
  return parse(
    await request('/api/v1/notifications/templates'),
    z.array(templateSchema),
  )
}

export async function saveNotificationTemplate(
  request: Requester,
  template: Pick<
    NotificationTemplate,
    'notificationType' | 'channel' | 'subjectTemplate' | 'bodyTemplate'
  >,
) {
  const response = await request('/api/v1/notifications/templates', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(template),
  })
  if (!response.ok) throw new Error('Unable to save notification template.')
}

export async function previewNotificationTemplate(
  request: Requester,
  template: Pick<
    NotificationTemplate,
    'notificationType' | 'channel' | 'subjectTemplate' | 'bodyTemplate'
  >,
) {
  return parse(
    await request('/api/v1/notifications/templates/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(template),
    }),
    previewSchema,
  )
}

export async function listFailedNotifications(request: Requester) {
  return parse(
    await request('/api/v1/notifications/failed'),
    z.array(failedSchema),
  )
}

export async function retryFailedNotification(
  request: Requester,
  deliveryId: string,
) {
  const response = await request(
    `/api/v1/notifications/failed/${deliveryId}/retry`,
    { method: 'POST' },
  )
  if (!response.ok) throw new Error('Unable to retry notification.')
}
