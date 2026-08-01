import { useState } from 'react'
import { Activity, Cpu, RotateCw, ShieldOff } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  createEdgeDeviceBootstrap,
  getEdgeDiagnostics,
  getEdgeLiveReading,
  listEdgeDevices,
  listEdgeSites,
  revokeEdgeDevice,
  rotateEdgeCredential,
  type EdgeDevice,
} from './edge-api'

export function EdgeDevicesPage() {
  const auth = useAuth()
  const client = useQueryClient()
  const canManage =
    auth.user?.permissions.includes('EdgeDevice.Manage') ?? false
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [issuedSecret, setIssuedSecret] = useState<{
    kind: string
    deviceId: string
    secret: string
    expiresAtUtc: string | null
  } | null>(null)
  const [form, setForm] = useState({
    name: '',
    siteId: '',
    weighbridgeName: '',
    weighbridgeCode: '',
    protocol: 'generic-line',
    parserVersion: 'generic-line-v1',
  })
  const devices = useQuery({
    queryKey: ['edge-devices'],
    queryFn: () => listEdgeDevices(auth.request),
    refetchInterval: 5000,
  })
  const sites = useQuery({
    queryKey: ['edge-sites'],
    queryFn: () => listEdgeSites(auth.request),
    enabled: canManage,
  })
  const diagnostics = useQuery({
    queryKey: ['edge-diagnostics', selectedId],
    queryFn: () => {
      if (!selectedId) {
        return Promise.reject(new Error('An edge device must be selected.'))
      }
      return getEdgeDiagnostics(auth.request, selectedId)
    },
    enabled: selectedId !== null,
  })
  const live = useQuery({
    queryKey: ['edge-live-reading', selectedId],
    queryFn: () => {
      if (!selectedId) {
        return Promise.reject(new Error('An edge device must be selected.'))
      }
      return getEdgeLiveReading(auth.request, selectedId)
    },
    enabled: selectedId !== null,
    refetchInterval: 2000,
  })
  const invalidate = () => {
    void client.invalidateQueries({ queryKey: ['edge-devices'] })
    void client.invalidateQueries({
      queryKey: ['edge-diagnostics', selectedId],
    })
  }
  const create = useMutation({
    mutationFn: () => createEdgeDeviceBootstrap(auth.request, form),
    onSuccess: (result) => {
      setIssuedSecret({
        kind: 'Bootstrap token',
        deviceId: result.deviceId,
        secret: result.bootstrapToken,
        expiresAtUtc: result.expiresAtUtc,
      })
      setForm({ ...form, name: '', weighbridgeName: '', weighbridgeCode: '' })
      invalidate()
    },
  })
  const rotate = useMutation({
    mutationFn: (deviceId: string) =>
      rotateEdgeCredential(auth.request, deviceId),
    onSuccess: (result) => {
      setIssuedSecret({
        kind: 'Rotated device secret',
        deviceId: result.deviceId,
        secret: result.secret,
        expiresAtUtc: result.expiresAtUtc,
      })
      invalidate()
    },
  })
  const revoke = useMutation({
    mutationFn: (deviceId: string) => revokeEdgeDevice(auth.request, deviceId),
    onSuccess: invalidate,
  })

  return (
    <section className="space-y-6" aria-labelledby="edge-devices-title">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-brand-700 text-xs font-bold tracking-[0.16em] uppercase">
            Edge foundation
          </p>
          <h1 id="edge-devices-title" className="mt-1 text-3xl font-black">
            Edge devices
          </h1>
          <p className="mt-1 max-w-3xl text-sm text-slate-600">
            Tenant- and site-bound agents submit structured readings. Hardware
            protocol parsing remains in the agent boundary.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => {
            void devices.refetch()
          }}
        >
          <RotateCw aria-hidden="true" size={16} /> Refresh
        </Button>
      </header>

      {issuedSecret ? (
        <section
          className="rounded-lg border border-amber-300 bg-amber-50 p-4"
          role="status"
        >
          <h2 className="font-bold text-amber-950">
            {issuedSecret.kind} shown once
          </h2>
          <p className="mt-1 text-sm text-amber-900">
            Copy the device ID and secret into the agent now. LoadIx does not
            display the secret again.
          </p>
          <dl className="mt-3 grid gap-2 text-sm text-amber-950 sm:grid-cols-2">
            <div>
              <dt className="font-semibold">Device ID</dt>
              <dd className="font-mono break-all">{issuedSecret.deviceId}</dd>
            </div>
            <div>
              <dt className="font-semibold">Expires</dt>
              <dd>{formatDate(issuedSecret.expiresAtUtc)}</dd>
            </div>
          </dl>
          <code className="mt-3 block rounded bg-white p-3 text-sm break-all text-slate-950">
            {issuedSecret.secret}
          </code>
          <Button
            className="mt-3"
            variant="outline"
            onClick={() => {
              setIssuedSecret(null)
            }}
          >
            Hide secret
          </Button>
        </section>
      ) : null}

      {canManage ? (
        <section className="rounded-lg border bg-white p-4">
          <h2 className="font-black">Register an edge installation</h2>
          <p className="mt-1 text-sm text-slate-600">
            The bootstrap token expires and can be used once.
          </p>
          <div className="mt-4 grid gap-3 md:grid-cols-2 lg:grid-cols-3">
            <label className="text-sm font-medium">
              Device name
              <input
                className="mt-1 min-h-11 w-full rounded-md border px-3"
                value={form.name}
                onChange={(event) => {
                  setForm({ ...form, name: event.target.value })
                }}
              />
            </label>
            <label className="text-sm font-medium">
              Assigned site
              <select
                className="mt-1 min-h-11 w-full rounded-md border px-3"
                value={form.siteId}
                onChange={(event) => {
                  setForm({ ...form, siteId: event.target.value })
                }}
              >
                <option value="">Select a site</option>
                {sites.data?.map((site) => (
                  <option key={site.id} value={site.id}>
                    {site.name} ({site.code})
                  </option>
                ))}
              </select>
            </label>
            <label className="text-sm font-medium">
              Weighbridge name
              <input
                className="mt-1 min-h-11 w-full rounded-md border px-3"
                value={form.weighbridgeName}
                onChange={(event) => {
                  setForm({ ...form, weighbridgeName: event.target.value })
                }}
              />
            </label>
            <label className="text-sm font-medium">
              Weighbridge code
              <input
                className="mt-1 min-h-11 w-full rounded-md border px-3"
                value={form.weighbridgeCode}
                onChange={(event) => {
                  setForm({ ...form, weighbridgeCode: event.target.value })
                }}
              />
            </label>
            <label className="text-sm font-medium">
              Protocol
              <input
                className="mt-1 min-h-11 w-full rounded-md border px-3"
                value={form.protocol}
                onChange={(event) => {
                  setForm({ ...form, protocol: event.target.value })
                }}
              />
            </label>
            <label className="text-sm font-medium">
              Parser version
              <input
                className="mt-1 min-h-11 w-full rounded-md border px-3"
                value={form.parserVersion}
                onChange={(event) => {
                  setForm({ ...form, parserVersion: event.target.value })
                }}
              />
            </label>
          </div>
          <Button
            className="mt-4"
            disabled={create.isPending}
            onClick={() => {
              create.mutate()
            }}
          >
            Create registration bootstrap
          </Button>
          {create.isError ? (
            <p className="mt-3 text-sm text-red-700" role="alert">
              {create.error.message}
            </p>
          ) : null}
        </section>
      ) : null}

      {devices.isError ? (
        <p className="text-sm text-red-700" role="alert">
          {devices.error.message}
        </p>
      ) : null}
      <section className="rounded-lg border bg-white p-4">
        <div className="flex items-center justify-between gap-3">
          <h2 className="font-black">Installations</h2>
          <p className="text-xs text-slate-500" aria-live="polite">
            {devices.data
              ? devices.data.totalCount.toString() + ' configured'
              : 'Loading...'}
          </p>
        </div>
        {devices.data?.items.length ? (
          <div className="mt-3 overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <caption className="sr-only">Edge device installations</caption>
              <thead>
                <tr className="border-b text-xs font-bold text-slate-500 uppercase">
                  <th className="px-3 py-2">Device</th>
                  <th className="px-3 py-2">Site</th>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2">Last heartbeat</th>
                  <th className="px-3 py-2">Actions</th>
                </tr>
              </thead>
              <tbody>
                {devices.data.items.map((device) => (
                  <DeviceRow
                    key={device.id}
                    device={device}
                    selected={selectedId === device.id}
                    canManage={canManage}
                    onSelect={() => {
                      setSelectedId(device.id)
                    }}
                    onRotate={() => {
                      rotate.mutate(device.id)
                    }}
                    onRevoke={() => {
                      revoke.mutate(device.id)
                    }}
                  />
                ))}
              </tbody>
            </table>
          </div>
        ) : devices.isPending ? (
          <p className="py-8 text-center text-sm text-slate-500">
            Loading devices...
          </p>
        ) : (
          <p className="py-8 text-center text-sm text-slate-500">
            No edge devices have been registered.
          </p>
        )}
      </section>

      {selectedId ? (
        <section className="grid gap-4 lg:grid-cols-2">
          <article className="rounded-lg border bg-slate-950 p-5 text-white">
            <div className="flex items-center gap-2">
              <Activity
                aria-hidden="true"
                size={18}
                className="text-emerald-300"
              />
              <h2 className="font-black">Live simulated reading</h2>
            </div>
            {live.data?.latestReading ? (
              <div className="mt-5">
                <p className="text-4xl font-black">
                  {live.data.latestReading.displayedValue}{' '}
                  <span className="text-xl text-slate-300">
                    {live.data.latestReading.unit}
                  </span>
                </p>
                <p className="mt-2 text-sm text-slate-300">
                  {live.data.latestReading.isStable ? 'Stable' : 'Unstable'} ·{' '}
                  {live.data.latestReading.vehicleRegistration ?? 'No vehicle'}
                </p>
                <p className="mt-1 text-xs text-slate-400">
                  Captured {formatDate(live.data.latestReading.capturedAtUtc)}
                </p>
              </div>
            ) : (
              <p className="mt-5 text-sm text-slate-300">
                Waiting for the simulator or an agent reading.
              </p>
            )}
          </article>
          <article className="rounded-lg border bg-white p-5">
            <h2 className="font-black">Diagnostic history</h2>
            {diagnostics.data ? (
              <div className="mt-3 space-y-2 text-sm">
                <p>
                  <span className="font-semibold">Status:</span>{' '}
                  {diagnostics.data.status}
                </p>
                <p>
                  <span className="font-semibold">Heartbeats:</span>{' '}
                  {diagnostics.data.heartbeats.length}
                </p>
                <p>
                  <span className="font-semibold">Commands:</span>{' '}
                  {diagnostics.data.commands.length}
                </p>
                <p>
                  <span className="font-semibold">Readings:</span>{' '}
                  {diagnostics.data.readings.length}
                </p>
                <div className="max-h-48 overflow-y-auto rounded-md bg-slate-50 p-3">
                  {diagnostics.data.auditEvents
                    .slice(0, 8)
                    .map((item, index) => (
                      <p className="border-b py-2 last:border-0" key={index}>
                        {item.eventType} · {formatDate(item.occurredAtUtc)}
                      </p>
                    ))}
                </div>
              </div>
            ) : (
              <p className="mt-3 text-sm text-slate-500">
                Loading diagnostics...
              </p>
            )}
          </article>
        </section>
      ) : null}
    </section>
  )
}

function DeviceRow({
  device,
  selected,
  canManage,
  onSelect,
  onRotate,
  onRevoke,
}: {
  device: EdgeDevice
  selected: boolean
  canManage: boolean
  onSelect: () => void
  onRotate: () => void
  onRevoke: () => void
}) {
  return (
    <tr className={selected ? 'bg-brand-50 border-b' : 'border-b'}>
      <td className="px-3 py-3">
        <button
          className="flex items-center gap-2 text-left font-bold text-slate-900 underline-offset-2 hover:underline"
          onClick={onSelect}
          type="button"
        >
          <Cpu aria-hidden="true" size={16} />
          {device.name}
        </button>
        <p className="mt-1 text-xs text-slate-500">
          {device.protocol} · {device.parserVersion}
        </p>
      </td>
      <td className="px-3 py-3">
        {device.siteName}
        <span className="block text-xs text-slate-500">{device.siteCode}</span>
      </td>
      <td className="px-3 py-3">
        <span
          className={
            device.isOnline
              ? 'rounded-full bg-emerald-100 px-2 py-1 text-xs font-bold text-emerald-800'
              : 'rounded-full bg-slate-100 px-2 py-1 text-xs font-bold text-slate-700'
          }
        >
          {device.isOnline ? 'Online' : device.status}
        </span>
      </td>
      <td className="px-3 py-3 text-xs text-slate-600">
        {formatDate(device.lastHeartbeatAtUtc)}
      </td>
      <td className="px-3 py-3">
        {canManage && device.status !== 'Revoked' ? (
          <div className="flex flex-wrap gap-2">
            <Button
              aria-label={'Rotate credential for ' + device.name}
              className="px-2"
              onClick={onRotate}
              type="button"
              variant="outline"
            >
              <RotateCw aria-hidden="true" size={15} />
            </Button>
            <Button
              aria-label={'Revoke ' + device.name}
              className="px-2 text-red-700"
              onClick={onRevoke}
              type="button"
              variant="outline"
            >
              <ShieldOff aria-hidden="true" size={15} />
            </Button>
          </div>
        ) : null}
      </td>
    </tr>
  )
}

function formatDate(value: unknown) {
  return typeof value === 'string' && value
    ? new Date(value).toLocaleString()
    : 'Never'
}
