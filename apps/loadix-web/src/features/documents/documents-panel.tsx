import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Camera,
  Download,
  Eye,
  FileText,
  RefreshCw,
  Trash2,
  UploadCloud,
} from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  deleteDocument,
  fetchDocumentBlob,
  listDocuments,
  uploadDocument,
} from '@/features/documents/document-api'
import type { DocumentItem } from '@/features/documents/document-api'

const tripDocumentTypes = [
  ['CollectionWeighbridgeTicket', 'Collection weighbridge ticket'],
  ['DeliveryWeighbridgeTicket', 'Delivery weighbridge ticket'],
  ['ProofOfDelivery', 'Proof of delivery'],
  ['InvoiceSupportingDocument', 'Invoice supporting document'],
  ['Other', 'Other'],
] as const

interface UploadFields {
  documentType: string
  notes: string
}

export function DocumentsPanel({
  tripId,
  tripStatus,
}: {
  tripId: string
  tripStatus: string
}) {
  const auth = useAuth()
  const client = useQueryClient()
  const fileInput = useRef<HTMLInputElement>(null)
  const cameraInput = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [progress, setProgress] = useState(0)
  const [operationKey, setOperationKey] = useState(crypto.randomUUID())
  const documents = useQuery({
    queryKey: ['documents', 'Trip', tripId],
    queryFn: () => listDocuments(auth.request, 'Trip', tripId),
  })
  const { register, handleSubmit, reset } = useForm<UploadFields>({
    defaultValues: {
      documentType: 'ProofOfDelivery',
      notes: '',
    },
  })
  const upload = useMutation({
    mutationFn: async (values: UploadFields) => {
      if (!file) throw new Error('Choose a document before uploading.')
      validateClientFile(file)
      return uploadDocument(
        auth.upload,
        {
          ownerType: 'Trip',
          ownerId: tripId,
          documentType: values.documentType,
          notes: values.notes,
          file,
          idempotencyKey: operationKey,
        },
        setProgress,
      )
    },
    onSuccess: () => {
      setFile(null)
      setProgress(100)
      setOperationKey(crypto.randomUUID())
      reset()
      if (fileInput.current) fileInput.current.value = ''
      if (cameraInput.current) cameraInput.current.value = ''
      void client.invalidateQueries({
        queryKey: ['documents', 'Trip', tripId],
      })
    },
  })
  const remove = useMutation({
    mutationFn: (document: DocumentItem) =>
      deleteDocument(auth.request, document),
    onSuccess: () =>
      client.invalidateQueries({
        queryKey: ['documents', 'Trip', tripId],
      }),
  })
  const immutable = tripStatus === 'Completed'
  const canUpload =
    !immutable &&
    (auth.user?.permissions.includes('Document.Manage') ||
      auth.user?.permissions.includes('Document.Driver'))

  function chooseFile(next: File | undefined) {
    if (!next) return
    setFile(next)
    setProgress(0)
    setOperationKey(crypto.randomUUID())
    upload.reset()
  }

  return (
    <section
      aria-labelledby="trip-documents-title"
      className="space-y-4 rounded-xl border bg-white p-5 shadow-sm"
    >
      <div>
        <h3 className="font-black text-slate-950" id="trip-documents-title">
          Operational documents
        </h3>
        <p className="text-sm text-slate-500">
          Originals are preserved. Image previews are orientation-corrected,
          size-constrained derivatives.
        </p>
      </div>

      {immutable ? (
        <div className="rounded-lg bg-slate-100 p-3 text-sm text-slate-700">
          This trip is completed. Its documents are immutable.
        </div>
      ) : null}

      {canUpload ? (
        <form
          className="space-y-3"
          onSubmit={(event) => {
            void handleSubmit((values) => upload.mutateAsync(values))(event)
          }}
        >
          <div
            className="rounded-xl border-2 border-dashed border-slate-300 bg-slate-50 p-5 text-center focus-within:border-blue-600"
            onDragOver={(event) => {
              event.preventDefault()
            }}
            onDrop={(event) => {
              event.preventDefault()
              chooseFile(event.dataTransfer.files[0])
            }}
          >
            <UploadCloud className="mx-auto text-blue-700" size={30} />
            <p className="mt-2 text-sm font-bold text-slate-900">
              Drop a PDF or image here
            </p>
            <p className="text-xs text-slate-500">
              PDF, JPEG, PNG, or WebP · maximum 15 MB
            </p>
            <div className="mt-3 flex flex-wrap justify-center gap-2">
              <Button
                onClick={() => fileInput.current?.click()}
                type="button"
                variant="outline"
              >
                Choose file
              </Button>
              <Button
                onClick={() => cameraInput.current?.click()}
                type="button"
                variant="outline"
              >
                <Camera size={17} /> Use camera
              </Button>
            </div>
            <input
              accept=".pdf,.jpg,.jpeg,.png,.webp"
              className="sr-only"
              onChange={(event) => {
                chooseFile(event.target.files?.[0])
              }}
              ref={fileInput}
              type="file"
            />
            <input
              accept="image/*"
              capture="environment"
              className="sr-only"
              onChange={(event) => {
                chooseFile(event.target.files?.[0])
              }}
              ref={cameraInput}
              type="file"
            />
          </div>

          {file ? (
            <div className="flex items-center gap-2 rounded-lg bg-blue-50 p-3 text-sm">
              <FileText className="shrink-0 text-blue-700" size={18} />
              <span className="min-w-0 flex-1 truncate font-semibold">
                {file.name}
              </span>
              <span className="text-slate-600">{formatBytes(file.size)}</span>
            </div>
          ) : null}

          <div className="grid gap-3 sm:grid-cols-2">
            <label className="text-sm font-semibold">
              Document type
              <select
                className="mt-1 min-h-11 w-full rounded-md border px-3 font-normal"
                {...register('documentType', { required: true })}
              >
                {tripDocumentTypes.map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-sm font-semibold">
              Notes (optional)
              <input
                className="mt-1 min-h-11 w-full rounded-md border px-3 font-normal"
                maxLength={1000}
                {...register('notes')}
              />
            </label>
          </div>

          {upload.isPending || progress > 0 ? (
            <div aria-live="polite">
              <div className="mb-1 flex justify-between text-xs font-semibold text-slate-600">
                <span>
                  {upload.isPending ? 'Uploading and scanning' : 'Uploaded'}
                </span>
                <span>{progress}%</span>
              </div>
              <progress
                aria-label="Document upload progress"
                className="h-2 w-full"
                max={100}
                value={progress}
              />
            </div>
          ) : null}

          {upload.isError ? (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-800">
              <span role="alert">{upload.error.message}</span>
              <Button
                disabled={!file || upload.isPending}
                onClick={() => {
                  void handleSubmit((values) => upload.mutateAsync(values))()
                }}
                type="button"
                variant="outline"
              >
                <RefreshCw size={16} /> Retry
              </Button>
            </div>
          ) : null}

          <Button disabled={!file || upload.isPending} type="submit">
            {upload.isPending ? 'Validating document…' : 'Upload document'}
          </Button>
        </form>
      ) : null}

      {documents.isPending ? (
        <p className="text-sm text-slate-600">Loading documents…</p>
      ) : documents.isError ? (
        <p
          className="rounded-lg bg-red-50 p-3 text-sm text-red-800"
          role="alert"
        >
          {documents.error.message}
        </p>
      ) : documents.data.length === 0 ? (
        <p className="rounded-lg bg-slate-50 p-4 text-sm text-slate-600">
          No documents have been uploaded for this trip.
        </p>
      ) : (
        <ul className="divide-y rounded-lg border">
          {documents.data.map((document) => (
            <DocumentRow
              document={document}
              deleting={remove.isPending}
              key={document.id}
              onDelete={() => {
                if (window.confirm(`Delete ${document.fileName}?`)) {
                  remove.mutate(document)
                }
              }}
            />
          ))}
        </ul>
      )}
    </section>
  )
}

function DocumentRow({
  document,
  deleting,
  onDelete,
}: {
  document: DocumentItem
  deleting: boolean
  onDelete: () => void
}) {
  const auth = useAuth()
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [error, setError] = useState('')

  useEffect(
    () => () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl)
    },
    [previewUrl],
  )

  async function preview() {
    setError('')
    try {
      const blob = await fetchDocumentBlob(auth.request, document, true)
      if (previewUrl) URL.revokeObjectURL(previewUrl)
      setPreviewUrl(URL.createObjectURL(blob))
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Preview failed.')
    }
  }

  async function download() {
    setError('')
    try {
      const blob = await fetchDocumentBlob(auth.request, document)
      const url = URL.createObjectURL(blob)
      const anchor = window.document.createElement('a')
      anchor.href = url
      anchor.download = document.fileName
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (value) {
      setError(value instanceof Error ? value.message : 'Download failed.')
    }
  }

  return (
    <li className="p-3">
      <div className="flex flex-wrap items-center gap-3">
        {previewUrl ? (
          <img
            alt={`Preview of ${document.fileName}`}
            className="h-16 w-16 rounded-md border object-cover"
            src={previewUrl}
          />
        ) : (
          <FileText className="text-slate-500" size={24} />
        )}
        <div className="min-w-48 flex-1">
          <p className="truncate text-sm font-bold text-slate-900">
            {document.fileName}
          </p>
          <p className="text-xs text-slate-500">
            {document.documentType} · {formatBytes(document.sizeBytes)} ·{' '}
            {new Date(document.uploadedAtUtc).toLocaleString()}
          </p>
          {document.notes ? (
            <p className="mt-1 text-sm text-slate-700">{document.notes}</p>
          ) : null}
        </div>
        <div className="flex gap-1">
          {document.hasPreview ? (
            <Button
              aria-label={`Preview ${document.fileName}`}
              onClick={() => void preview()}
              variant="ghost"
            >
              <Eye size={17} />
            </Button>
          ) : null}
          <Button
            aria-label={`Download ${document.fileName}`}
            onClick={() => void download()}
            variant="ghost"
          >
            <Download size={17} />
          </Button>
          {document.canDelete ? (
            <Button
              aria-label={`Delete ${document.fileName}`}
              disabled={deleting}
              onClick={onDelete}
              variant="ghost"
            >
              <Trash2 size={17} />
            </Button>
          ) : null}
        </div>
      </div>
      {error ? (
        <p className="mt-2 text-sm text-red-700" role="alert">
          {error}
        </p>
      ) : null}
    </li>
  )
}

function validateClientFile(file: File) {
  const extension = file.name.split('.').pop()?.toLowerCase()
  if (
    !extension ||
    !['pdf', 'jpg', 'jpeg', 'png', 'webp'].includes(extension)
  ) {
    throw new Error('Choose a PDF, JPEG, PNG, or WebP file.')
  }
  if (file.size <= 0 || file.size > 15 * 1024 * 1024) {
    throw new Error('The file must be larger than zero and no more than 15 MB.')
  }
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${String(bytes)} B`
  if (bytes < 1024 * 1024) return `${String(Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
