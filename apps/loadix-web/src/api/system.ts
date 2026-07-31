import { z } from 'zod'
import { environment } from '@/config/env'

const systemInfoSchema = z.object({
  serviceName: z.string(),
  version: z.string(),
  apiVersion: z.string(),
  serverTimeUtc: z.iso.datetime({ offset: true }),
})

export type SystemInfo = z.infer<typeof systemInfoSchema>

export async function getSystemInfo(signal?: AbortSignal): Promise<SystemInfo> {
  const response = await fetch(
    `${environment.VITE_API_BASE_URL}/v1/system/info`,
    {
      headers: { Accept: 'application/json' },
      ...(signal ? { signal } : {}),
    },
  )

  if (!response.ok) {
    throw new Error(`The API returned HTTP ${String(response.status)}.`)
  }

  return systemInfoSchema.parse(await response.json())
}
