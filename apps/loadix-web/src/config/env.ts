import { z } from 'zod'

const environmentSchema = z.object({
  VITE_API_BASE_URL: z.string().min(1).default('/api'),
})

const parsedEnvironment = environmentSchema.safeParse(import.meta.env)

if (!parsedEnvironment.success) {
  throw new Error('The web application environment configuration is invalid.')
}

export const environment = parsedEnvironment.data
