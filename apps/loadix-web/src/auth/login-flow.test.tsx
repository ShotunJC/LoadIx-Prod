import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { App } from '@/App'

describe('Authentication flow', () => {
  it('logs in and opens the protected dashboard', async () => {
    window.history.pushState({}, '', '/login')
    const fetchMock = vi
      .fn()
      .mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
        const url =
          typeof input === 'string'
            ? input
            : input instanceof URL
              ? input.href
              : input.url
        if (url.includes('/auth/refresh')) {
          return Promise.resolve({ ok: false, status: 401 })
        }
        if (url.includes('/auth/login') && init?.method === 'POST') {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () =>
              Promise.resolve({
                accessToken: 'memory-only-token',
                expiresAtUtc: '2026-07-28T09:00:00+00:00',
                sessionId: '30000000-0000-0000-0000-000000000001',
                user: {
                  id: '20000000-0000-0000-0000-000000000001',
                  tenantId: '10000000-0000-0000-0000-000000000001',
                  email: 'admin@loadix.example.invalid',
                  displayName: 'Demo Administrator',
                  permissions: ['User.View'],
                  roles: [],
                  isPlatformAdministrator: false,
                  isSupportContext: false,
                },
              }),
          })
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () =>
            Promise.resolve({
              serviceName: 'LoadIx.Api',
              version: '0.1.0',
              apiVersion: 'v1',
              serverTimeUtc: '2026-07-28T08:30:00+00:00',
            }),
        })
      })
    vi.stubGlobal('fetch', fetchMock)

    render(<App />)
    const user = userEvent.setup()
    await user.type(
      await screen.findByRole('textbox', { name: 'Email' }),
      'admin@loadix.example.invalid',
    )
    await user.type(screen.getByLabelText('Password'), 'test-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(
      await screen.findByRole('heading', { name: 'Operations dashboard' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/auth/login',
      expect.objectContaining({ method: 'POST' }),
    )
  })
})
