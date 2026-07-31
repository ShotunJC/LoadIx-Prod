import { describe, expect, it, vi } from 'vitest'
import {
  listFailedNotifications,
  listNotificationPreferences,
  listNotifications,
  previewNotificationTemplate,
  saveNotificationPreference,
} from './notification-api'

describe('notification API client', () => {
  it('loads inbox, preferences, templates, and failed delivery data', async () => {
    const request = vi.fn((input: RequestInfo | URL) => {
      const url =
        typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.href
            : input.url
      if (url.endsWith('/notifications')) {
        return json({
          items: [
            {
              id: '10000000-0000-0000-0000-000000000001',
              type: 'TripAssignment',
              title: 'Trip assigned',
              body: 'LX-0001 assigned',
              createdAtUtc: '2026-07-30T10:00:00+00:00',
              readAtUtc: null,
            },
          ],
          unreadCount: 1,
        })
      }
      if (url.endsWith('/preferences')) {
        return json([
          {
            notificationType: 'TripAssignment',
            inAppEnabled: true,
            emailEnabled: true,
            smsEnabled: false,
          },
        ])
      }
      if (url.endsWith('/failed')) {
        return json([
          {
            deliveryId: '20000000-0000-0000-0000-000000000001',
            notificationType: 'TripAssignment',
            channel: 'email',
            attempts: 5,
            providerCorrelationId: null,
            failure: 'SMTP unavailable',
            nextAttemptAtUtc: '2026-07-30T10:05:00+00:00',
          },
        ])
      }
      return json({ subject: 'Trip assigned', body: 'LX-0001 assigned' })
    })

    expect((await listNotifications(request)).unreadCount).toBe(1)
    expect(await listNotificationPreferences(request)).toHaveLength(1)
    expect(await listFailedNotifications(request)).toHaveLength(1)
    expect(
      await previewNotificationTemplate(request, {
        notificationType: 'TripAssignment',
        channel: 'email',
        subjectTemplate: '{{Title}}',
        bodyTemplate: '{{Body}}',
      }),
    ).toEqual({ subject: 'Trip assigned', body: 'LX-0001 assigned' })
  })

  it('saves user preferences through a typed request body', async () => {
    const request = vi.fn(() =>
      Promise.resolve(new Response(null, { status: 204 })),
    )

    await saveNotificationPreference(request, {
      notificationType: 'Dispatch',
      inAppEnabled: true,
      emailEnabled: false,
      smsEnabled: true,
    })

    expect(request).toHaveBeenCalledWith(
      '/api/v1/notifications/preferences',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({
          notificationType: 'Dispatch',
          inAppEnabled: true,
          emailEnabled: false,
          smsEnabled: true,
        }),
      }),
    )
  })
})

function json(value: unknown) {
  return Promise.resolve(
    new Response(JSON.stringify(value), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }),
  )
}
