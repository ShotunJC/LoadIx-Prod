import { type SyntheticEvent, useState } from 'react'
import { Link, useSearch } from '@tanstack/react-router'
import { AuthCard } from '@/components/auth-card'
import { Button } from '@/components/ui/button'

export function AcceptInvitationPage() {
  const search = useSearch({ from: '/accept-invitation' })
  const [complete, setComplete] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const response = await fetch('/api/v1/auth/accept-invitation', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        token: search.token,
        password: data.get('password'),
      }),
    })
    if (!response.ok) {
      setError('The invitation is invalid, expired, or already used.')
      return
    }
    setComplete(true)
  }

  return (
    <AuthCard
      description="Invitation links are single-use and expire after seven days."
      title="Accept your invitation"
    >
      {complete ? (
        <p className="rounded-lg bg-emerald-50 p-4 text-emerald-900">
          Account created.{' '}
          <Link className="font-bold underline" to="/login">
            Sign in
          </Link>
        </p>
      ) : (
        <form
          className="space-y-5"
          onSubmit={(event) => {
            void submit(event)
          }}
        >
          {error ? <p className="text-sm text-red-700">{error}</p> : null}
          <label className="block text-sm font-semibold">
            Password
            <input
              autoComplete="new-password"
              className="mt-2 w-full rounded-lg border px-3 py-2.5 font-normal"
              minLength={12}
              name="password"
              required
              type="password"
            />
          </label>
          <Button className="w-full" type="submit">
            Create account
          </Button>
        </form>
      )}
    </AuthCard>
  )
}
