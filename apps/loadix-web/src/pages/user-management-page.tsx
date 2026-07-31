import { type SyntheticEvent, useCallback, useEffect, useState } from 'react'
import { Navigate } from '@tanstack/react-router'
import { useAuth, type UserRole } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'

interface Role extends UserRole {
  permissions: string[]
}

interface TenantUser {
  id: string
  email: string
  displayName: string
  isActive: boolean
  emailConfirmed: boolean
  roles: UserRole[]
}

export function UserManagementPage() {
  const auth = useAuth()
  const [users, setUsers] = useState<TenantUser[]>([])
  const [roles, setRoles] = useState<Role[]>([])
  const [message, setMessage] = useState('')

  const load = useCallback(async () => {
    const [usersResponse, rolesResponse] = await Promise.all([
      auth.request('/api/v1/users'),
      auth.request('/api/v1/users/roles'),
    ])
    if (usersResponse.ok && rolesResponse.ok) {
      setUsers((await usersResponse.json()) as TenantUser[])
      setRoles((await rolesResponse.json()) as Role[])
    }
  }, [auth])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load()
    }, 0)
    return () => {
      window.clearTimeout(timer)
    }
  }, [load])

  if (!auth.user?.permissions.includes('User.View')) {
    return <Navigate replace to="/access-denied" />
  }

  async function invite(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const response = await auth.request('/api/v1/users/invitations', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email: data.get('email'),
        displayName: data.get('displayName'),
        roleIds: data.getAll('roleId'),
      }),
    })
    setMessage(
      response.ok
        ? 'Invitation sent through the configured email service.'
        : 'The invitation could not be sent.',
    )
    if (response.ok) {
      form.reset()
    }
  }

  async function assignRoles(
    userId: string,
    event: SyntheticEvent<HTMLFormElement>,
  ) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const response = await auth.request(`/api/v1/users/${userId}/roles`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ roleIds: data.getAll('roleId') }),
    })
    setMessage(response.ok ? 'Roles updated.' : 'Roles could not be updated.')
    if (response.ok) {
      await load()
    }
  }

  return (
    <div className="space-y-7">
      <header>
        <p className="text-brand-700 text-xs font-bold tracking-widest uppercase">
          Tenant administration
        </p>
        <h1 className="mt-2 text-3xl font-black text-slate-950">Users</h1>
        <p className="mt-2 text-slate-600">
          Invite users and assign roles within your current authority.
        </p>
      </header>

      {message ? (
        <p
          role="status"
          className="rounded-lg bg-slate-900 p-3 text-sm text-white"
        >
          {message}
        </p>
      ) : null}

      {auth.user.permissions.includes('User.Manage') ? (
        <section className="rounded-xl border bg-white p-5 shadow-sm">
          <h2 className="text-lg font-black">Invite a user</h2>
          <form
            className="mt-4 grid gap-4 lg:grid-cols-2"
            onSubmit={(event) => {
              void invite(event)
            }}
          >
            <label className="text-sm font-semibold">
              Display name
              <input
                className="mt-1.5 w-full rounded-lg border px-3 py-2"
                name="displayName"
                required
              />
            </label>
            <label className="text-sm font-semibold">
              Email
              <input
                className="mt-1.5 w-full rounded-lg border px-3 py-2"
                name="email"
                required
                type="email"
              />
            </label>
            <fieldset className="lg:col-span-2">
              <legend className="text-sm font-semibold">Roles</legend>
              <div className="mt-2 flex flex-wrap gap-3">
                {roles.map((role) => (
                  <label
                    className="rounded-lg border px-3 py-2 text-sm"
                    key={role.id}
                  >
                    <input
                      className="mr-2"
                      name="roleId"
                      type="checkbox"
                      value={role.id}
                    />
                    {role.name}
                  </label>
                ))}
              </div>
            </fieldset>
            <Button className="w-fit" type="submit">
              Send invitation
            </Button>
          </form>
        </section>
      ) : null}

      <section className="overflow-hidden rounded-xl border bg-white shadow-sm">
        <h2 className="border-b px-5 py-4 text-lg font-black">Tenant users</h2>
        <div className="divide-y">
          {users.map((user) => (
            <article className="p-5" key={user.id}>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="font-bold text-slate-950">
                    {user.displayName}
                  </h3>
                  <p className="text-sm text-slate-600">{user.email}</p>
                </div>
                <span
                  className={
                    user.isActive
                      ? 'rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-800'
                      : 'rounded-full bg-slate-200 px-3 py-1 text-xs font-bold text-slate-700'
                  }
                >
                  {user.isActive ? 'Active' : 'Inactive'}
                </span>
              </div>
              {auth.user?.permissions.includes('User.Manage') ? (
                <form
                  className="mt-4 flex flex-wrap items-end gap-3"
                  onSubmit={(event) => {
                    void assignRoles(user.id, event)
                  }}
                >
                  {roles.map((role) => (
                    <label className="text-sm" key={role.id}>
                      <input
                        className="mr-1.5"
                        defaultChecked={user.roles.some(
                          (assigned) => assigned.id === role.id,
                        )}
                        name="roleId"
                        type="checkbox"
                        value={role.id}
                      />
                      {role.name}
                    </label>
                  ))}
                  <Button type="submit" variant="outline">
                    Save roles
                  </Button>
                </form>
              ) : null}
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
