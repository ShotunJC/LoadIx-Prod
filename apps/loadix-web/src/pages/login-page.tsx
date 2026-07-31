import { type SyntheticEvent, useState } from 'react'
import { Link, useNavigate } from '@tanstack/react-router'
import { useAuth } from '@/auth/auth-context'
import { AuthCard } from '@/components/auth-card'
import { Button } from '@/components/ui/button'

export function LoginPage({ redirectTo }: { redirectTo?: string }) {
  const auth = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    setError('')
    const data = new FormData(event.currentTarget)
    const email = data.get('email')
    const password = data.get('password')
    if (typeof email !== 'string' || typeof password !== 'string') {
      setSubmitting(false)
      return
    }
    try {
      await auth.login(email, password)
      const queryRedirect = new URLSearchParams(window.location.search).get(
        'redirect',
      )
      await navigate({ to: safeRedirect(redirectTo ?? queryRedirect ?? '/') })
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Login failed.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthCard
      description="Use the account supplied by your LoadIx administrator."
      title="Sign in to LoadIx"
    >
      {auth.sessionExpired ? (
        <p className="mb-4 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">
          Your session expired. Sign in again to continue.
        </p>
      ) : null}
      {error ? (
        <p className="mb-4 rounded-lg bg-red-50 p-3 text-sm text-red-800">
          {error}
        </p>
      ) : null}
      <form
        className="space-y-5"
        onSubmit={(event) => {
          void submit(event)
        }}
      >
        <label className="block text-sm font-semibold text-slate-800">
          Email
          <input
            autoComplete="username"
            className="mt-2 w-full rounded-lg border px-3 py-2.5 font-normal"
            name="email"
            required
            type="email"
          />
        </label>
        <label className="block text-sm font-semibold text-slate-800">
          Password
          <input
            autoComplete="current-password"
            className="mt-2 w-full rounded-lg border px-3 py-2.5 font-normal"
            name="password"
            required
            type="password"
          />
        </label>
        <Button className="w-full" disabled={submitting} type="submit">
          {submitting ? 'Signing in…' : 'Sign in'}
        </Button>
      </form>
      <Link
        className="text-brand-700 mt-5 inline-block text-sm font-semibold"
        to="/forgot-password"
      >
        Forgot your password?
      </Link>
    </AuthCard>
  )
}

function safeRedirect(value: string | undefined) {
  return value?.startsWith('/') && !value.startsWith('//') ? value : '/'
}
