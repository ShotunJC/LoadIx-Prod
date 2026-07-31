import { z } from 'zod'

const guid = z
  .string()
  .regex(
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
    'Invalid GUID',
  )

const documentSchema = z.object({
  id: guid,
  ownerType: z.string(),
  ownerId: guid,
  documentType: z.string(),
  fileName: z.string(),
  contentType: z.string(),
  sizeBytes: z.number(),
  sha256Checksum: z.string(),
  uploaderUserId: guid,
  uploadedAtUtc: z.string(),
  notes: z.string(),
  hasPreview: z.boolean(),
  canDelete: z.boolean(),
  version: z.string(),
})

const documentListSchema = z.array(documentSchema)

export type DocumentItem = z.infer<typeof documentSchema>

type Requester = (
  input: RequestInfo | URL,
  init?: RequestInit,
) => Promise<Response>

type Uploader = (
  input: string,
  body: FormData,
  headers: Record<string, string>,
  onProgress: (percent: number) => void,
) => Promise<Response>

async function problem(response: Response) {
  const value = (await response.json().catch(() => null)) as {
    detail?: string
  } | null
  return value?.detail ?? 'The document request could not be completed.'
}

export async function listDocuments(
  request: Requester,
  ownerType: string,
  ownerId: string,
) {
  const query = new URLSearchParams({ ownerType, ownerId })
  const response = await request(`/api/v1/documents?${query}`)
  if (!response.ok) throw new Error(await problem(response))
  return documentListSchema.parse(await response.json())
}

export async function uploadDocument(
  upload: Uploader,
  values: {
    ownerType: string
    ownerId: string
    documentType: string
    notes: string
    file: File
    idempotencyKey: string
  },
  onProgress: (percent: number) => void,
) {
  const form = new FormData()
  form.set('ownerType', values.ownerType)
  form.set('ownerId', values.ownerId)
  form.set('documentType', values.documentType)
  form.set('notes', values.notes)
  form.set('file', values.file, values.file.name)
  const response = await upload(
    '/api/v1/documents',
    form,
    { 'Idempotency-Key': values.idempotencyKey },
    onProgress,
  )
  if (!response.ok) throw new Error(await problem(response))
  return documentSchema.parse(await response.json())
}

export async function deleteDocument(
  request: Requester,
  document: DocumentItem,
) {
  const response = await request(`/api/v1/documents/${document.id}`, {
    method: 'DELETE',
    headers: { 'If-Match': `"${document.version}"` },
  })
  if (!response.ok) throw new Error(await problem(response))
}

export async function fetchDocumentBlob(
  request: Requester,
  document: DocumentItem,
  preview = false,
) {
  const response = await request(
    `/api/v1/documents/${document.id}/${preview ? 'preview' : 'content'}`,
  )
  if (!response.ok) throw new Error(await problem(response))
  return response.blob()
}
