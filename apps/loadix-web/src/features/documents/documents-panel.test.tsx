import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { DocumentsPanel } from './documents-panel'

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  upload: vi.fn(),
}))

vi.mock('@/auth/auth-context', () => ({
  useAuth: () => ({
    user: {
      permissions: ['Document.View', 'Document.Manage'],
    },
    request: mocks.request,
    upload: mocks.upload,
  }),
}))

describe('DocumentsPanel', () => {
  it('makes completed-trip evidence immutable while retaining downloads', async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      json: () =>
        Promise.resolve([
          {
            id: '74000000-0000-4000-8000-000000000001',
            ownerType: 'Trip',
            ownerId: '74000000-0000-4000-8000-000000000002',
            documentType: 'ProofOfDelivery',
            fileName: 'pod.pdf',
            contentType: 'application/pdf',
            sizeBytes: 512,
            sha256Checksum: 'A'.repeat(64),
            uploaderUserId: '74000000-0000-4000-8000-000000000003',
            uploadedAtUtc: '2026-07-29T10:00:00Z',
            notes: 'Signed delivery evidence',
            hasPreview: false,
            canDelete: false,
            version: 'AAAA',
          },
        ]),
    })
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })

    render(
      <QueryClientProvider client={queryClient}>
        <DocumentsPanel
          tripId="74000000-0000-4000-8000-000000000002"
          tripStatus="Completed"
        />
      </QueryClientProvider>,
    )

    expect(
      await screen.findByText(
        'This trip is completed. Its documents are immutable.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Choose file' })).toBeNull()
    expect(
      await screen.findByRole('button', { name: 'Download pod.pdf' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete pod.pdf' })).toBeNull()
  })
})
