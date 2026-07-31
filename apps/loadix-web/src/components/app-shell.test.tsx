import { render, screen } from '@testing-library/react'
import { vi } from 'vitest'
import { App } from '@/App'

const authenticatedUser = {
  id: '20000000-0000-0000-0000-000000000001',
  tenantId: '10000000-0000-0000-0000-000000000001',
  email: 'admin@loadix.example.invalid',
  displayName: 'Demo Administrator',
  permissions: ['User.View', 'User.Manage'],
  roles: [{ id: '1', name: 'Tenant Administrator' }],
  isPlatformAdministrator: false,
  isSupportContext: false,
}

vi.stubGlobal(
  'fetch',
  vi.fn().mockImplementation((input: RequestInfo | URL) => {
    const url =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : input.url
    return Promise.resolve({
      ok: true,
      status: 200,
      json: () =>
        Promise.resolve(
          url.includes('/auth/refresh')
            ? {
                accessToken: 'test-token',
                expiresAtUtc: '2026-07-28T09:00:00+00:00',
                sessionId: '30000000-0000-0000-0000-000000000001',
                user: authenticatedUser,
              }
            : {
                serviceName: 'LoadIx.Api',
                version: '0.1.0',
                apiVersion: 'v1',
                serverTimeUtc: '2026-07-28T08:30:00+00:00',
              },
        ),
    })
  }),
)

describe('App shell', () => {
  it('renders accessible navigation and the dashboard', async () => {
    render(<App />)

    expect(
      await screen.findByRole('heading', { name: 'Operations dashboard' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('navigation', { name: 'Primary navigation' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Skip to content' }),
    ).toHaveAttribute('href', '#main-content')
    expect(await screen.findByText('API connected')).toBeInTheDocument()
  })
})
