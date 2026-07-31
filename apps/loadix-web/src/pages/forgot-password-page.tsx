import { type SyntheticEvent, useState } from 'react'
import { AuthCard } from '@/components/auth-card'
import { Button } from '@/components/ui/button'

export function ForgotPasswordPage() {
  const [sent, setSent] = useState(false)

  async function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    await fetch('/api/v1/auth/forgot-password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: data.get('email') }),
    })
    setSent(true)
  }

  return (
    <AuthCard
      description="We use the same response whether or not an account exists."
      title="Reset your password"
    >
      {sent ? (
        <p
          role="status"
          className="rounded-lg bg-emerald-50 p-4 text-emerald-900"
        >
          If the account is eligible, reset instructions have been sent.
        </p>
      ) : (
        <form
          className="space-y-5"
          onSubmit={(event) => {
            void submit(event)
          }}
        >
          <label className="block text-sm font-semibold">
            Email
            <input
              className="mt-2 w-full rounded-lg border px-3 py-2.5 font-normal"
              name="email"
              required
              type="email"
            />
          </label>
          <Button className="w-full" type="submit">
            Send reset instructions
          </Button>
        </form>
      )}
    </AuthCard>
  )
}
