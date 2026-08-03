import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type Dispatch,
  type SetStateAction,
} from 'react'
import {
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr'
import { Map as MapLibreMap, Marker } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Maximize2, RefreshCw } from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const columns = [
  ['Planned', ['Planned']],
  ['Awaiting assignment', ['Assigned']],
  ['Dispatched', ['Dispatched', 'Loading', 'CollectionComplete']],
  ['At collection', ['AtCollection']],
  ['In transit', ['InTransit']],
  ['At delivery', ['AtDelivery', 'Unloading', 'DeliveryComplete']],
  ['Reconciliation exception', ['ReconciliationRequired']],
  ['Completed today', ['Completed']],
] as const

type Requester = (
  input: RequestInfo | URL,
  init?: RequestInit,
) => Promise<Response>

type BoardItem = {
  tripId: string
  tripNumber: string
  customerId: string
  customerName: string
  commodityId: string
  commodityName: string
  routeReference: string
  driverId: string | null
  driverName: string | null
  vehicleId: string | null
  vehicleReference: string | null
  branchId: string | null
  branchName: string | null
  status: string
  plannedCollectionAtUtc: string
  plannedDeliveryAtUtc: string
  hasException: boolean
  isDelayed: boolean
  completedAtUtc: string | null
}

type BoardResponse = { items: BoardItem[]; lastUpdatedAtUtc: string }

type LocationItem = {
  tripId: string
  tripNumber: string
  status: string
  latitude: number
  longitude: number
  capturedAtUtc: string
  accuracyMetres: number | null
}

type Filters = {
  search: string
  customerId: string
  branchId: string
  driverId: string
  vehicleId: string
  commodityId: string
  delayedOnly: boolean
  exceptionOnly: boolean
}

const emptyFilters: Filters = {
  search: '',
  customerId: '',
  branchId: '',
  driverId: '',
  vehicleId: '',
  commodityId: '',
  delayedOnly: false,
  exceptionOnly: false,
}

export function OperationsPage() {
  const auth = useAuth()
  const getAccessToken = auth.getAccessToken
  const client = useQueryClient()
  const [filters, setFilters] = useState(emptyFilters)
  const [connected, setConnected] = useState(false)
  const [fullScreen, setFullScreen] = useState(false)
  const board = useQuery({
    queryKey: ['operations-board', filters],
    queryFn: () => fetchBoard(auth.request, filters),
    refetchInterval: connected ? false : 30_000,
  })
  const refresh = useCallback(async () => {
    await client.invalidateQueries({ queryKey: ['operations-board'] })
    await client.invalidateQueries({ queryKey: ['operations-locations'] })
  }, [client])

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(new URL('/hubs/operations', window.location.origin).toString(), {
        accessTokenFactory: () => getAccessToken() ?? '',
        transport: import.meta.env.DEV
          ? HttpTransportType.LongPolling
          : HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connection.on('operations.event', () => {
      void refresh()
    })
    connection.onreconnected(() => {
      setConnected(true)
      void refresh()
    })
    connection.onreconnecting(() => {
      setConnected(false)
    })
    connection.onclose(() => {
      setConnected(false)
    })
    let disposed = false
    const start = startConnection(connection, () => disposed)
      .then(() => {
        if (!disposed) setConnected(true)
      })
      .catch(() => {
        if (!disposed) setConnected(false)
      })
    void start.then(() => {
      if (disposed) void connection.stop()
    })
    return () => {
      disposed = true
      if (connection.state !== HubConnectionState.Connecting) {
        void connection.stop()
      }
    }
  }, [getAccessToken, refresh])

  const items = useMemo(() => board.data?.items ?? [], [board.data?.items])
  const options = useMemo(() => buildOptions(items), [items])
  const lastUpdated = board.data?.lastUpdatedAtUtc
    ? new Date(board.data.lastUpdatedAtUtc)
    : null

  return (
    <section
      className={cn(
        'space-y-5',
        fullScreen && 'fixed inset-0 z-50 overflow-auto bg-slate-100 p-5',
      )}
      aria-labelledby="operations-title"
    >
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-brand-700 text-xs font-bold tracking-widest uppercase">
            Live operations
          </p>
          <h1 className="text-3xl font-black" id="operations-title">
            Control tower
          </h1>
          <p className="text-sm text-slate-600">
            {connected ? 'Live connection active' : 'Manual refresh fallback'} ·
            Updated {lastUpdated?.toLocaleTimeString() ?? 'pending'}
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            className="gap-2"
            onClick={() => {
              setFullScreen((value) => !value)
            }}
            type="button"
            variant="outline"
          >
            <Maximize2 aria-hidden="true" size={16} />
            Full screen
          </Button>
          <Button
            className="gap-2"
            onClick={() => void refresh()}
            type="button"
          >
            <RefreshCw aria-hidden="true" size={16} />
            Refresh
          </Button>
        </div>
      </header>
      <FilterBar filters={filters} options={options} setFilters={setFilters} />
      <OperationsMap request={auth.request} />
      {board.isError ? (
        <p className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm font-semibold text-red-800">
          The operations board could not be loaded.
        </p>
      ) : null}
      <div className="grid gap-3 xl:grid-cols-4">
        {columns.map(([label, statuses]) => {
          const columnItems = items.filter((trip) =>
            (statuses as readonly string[]).includes(trip.status),
          )
          return (
            <section
              className="min-h-44 rounded-xl bg-slate-100 p-3"
              key={label}
            >
              <h2 className="mb-3 flex items-center justify-between gap-3 font-black text-slate-900">
                {label}
                <span className="rounded-full bg-white px-2 py-0.5 text-xs">
                  {columnItems.length}
                </span>
              </h2>
              <div className="space-y-2">
                {columnItems.length ? (
                  columnItems.map((trip) => (
                    <TripCard key={trip.tripId} trip={trip} />
                  ))
                ) : (
                  <p className="rounded-lg border border-dashed border-slate-300 p-3 text-sm text-slate-500">
                    No trips in this stage.
                  </p>
                )}
              </div>
            </section>
          )
        })}
      </div>
    </section>
  )
}

async function startConnection(
  connection: ReturnType<HubConnectionBuilder['build']>,
  isDisposed: () => boolean,
) {
  let attempt = 0
  while (!isDisposed()) {
    try {
      await connection.start()
      return
    } catch {
      attempt += 1
      const delayMilliseconds = Math.min(
        30_000,
        1_000 * 2 ** Math.min(attempt, 5),
      )
      await new Promise((resolve) => setTimeout(resolve, delayMilliseconds))
    }
  }
}

function FilterBar({
  filters,
  options,
  setFilters,
}: {
  filters: Filters
  options: Record<string, Array<{ id: string; label: string }>>
  setFilters: Dispatch<SetStateAction<Filters>>
}) {
  return (
    <div className="grid gap-3 rounded-xl border bg-white p-4 md:grid-cols-4">
      <input
        aria-label="Search operations"
        className="min-h-11 rounded-md border px-3"
        onChange={(event) => {
          setFilters((current) => ({ ...current, search: event.target.value }))
        }}
        placeholder="Trip, route, contract"
        value={filters.search}
      />
      {(
        [
          'customerId',
          'branchId',
          'driverId',
          'vehicleId',
          'commodityId',
        ] as const
      ).map((key) => (
        <select
          className="min-h-11 rounded-md border px-3"
          key={key}
          onChange={(event) => {
            setFilters((current) => ({
              ...current,
              [key]: event.target.value,
            }))
          }}
          value={filters[key]}
        >
          <option value="">All {key.replace('Id', '').toLowerCase()}s</option>
          {(options[key] ?? []).map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      ))}
      <label className="flex items-center gap-2 text-sm font-semibold">
        <input
          checked={filters.delayedOnly}
          onChange={(event) => {
            setFilters((current) => ({
              ...current,
              delayedOnly: event.target.checked,
            }))
          }}
          type="checkbox"
        />
        Delayed only
      </label>
      <label className="flex items-center gap-2 text-sm font-semibold">
        <input
          checked={filters.exceptionOnly}
          onChange={(event) => {
            setFilters((current) => ({
              ...current,
              exceptionOnly: event.target.checked,
            }))
          }}
          type="checkbox"
        />
        Exceptions only
      </label>
    </div>
  )
}

function TripCard({ trip }: { trip: BoardItem }) {
  return (
    <article className="rounded-lg border bg-white p-3 text-sm shadow-sm">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="font-black">{trip.tripNumber}</p>
          <p>{trip.customerName}</p>
        </div>
        <StatusBadge trip={trip} />
      </div>
      <p className="mt-2 text-slate-600">
        {trip.driverName ?? 'Unassigned'} ·{' '}
        {trip.vehicleReference ?? 'No vehicle'}
      </p>
      <p className="text-xs text-slate-500">{trip.commodityName}</p>
    </article>
  )
}

function StatusBadge({ trip }: { trip: BoardItem }) {
  if (trip.hasException) {
    return (
      <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-black text-amber-900">
        Exception
      </span>
    )
  }
  if (trip.isDelayed) {
    return (
      <span className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-black text-red-800">
        Delayed
      </span>
    )
  }
  return (
    <span className="bg-brand-100 text-brand-800 rounded-full px-2 py-0.5 text-xs font-black">
      {trip.status}
    </span>
  )
}

function OperationsMap({ request }: { request: Requester }) {
  const mapRef = useRef<MapLibreMap | null>(null)
  const markersRef = useRef<Marker[]>([])
  const locations = useQuery({
    queryKey: ['operations-locations'],
    queryFn: () => fetchLocations(request),
    refetchInterval: 60_000,
  })

  const ref = (node: HTMLDivElement | null) => {
    if (!node || mapRef.current) return
    mapRef.current = new MapLibreMap({
      container: node,
      style: 'https://demotiles.maplibre.org/style.json',
      center: [24.7, -28.5],
      zoom: 4,
    })
  }

  useEffect(() => {
    markersRef.current.forEach((marker) => marker.remove())
    markersRef.current = []
    if (!mapRef.current) return
    for (const location of locations.data ?? []) {
      const marker = new Marker({
        color: freshnessColor(location.capturedAtUtc),
      })
        .setLngLat([location.longitude, location.latitude])
        .addTo(mapRef.current)
      marker
        .getElement()
        .setAttribute('aria-label', `${location.tripNumber} position`)
      markersRef.current.push(marker)
    }
  }, [locations.data])

  return (
    <section className="rounded-xl border bg-white p-4">
      <h2 className="font-black">Map</h2>
      <div
        aria-label="Operations map"
        className="mt-3 h-80 rounded-lg"
        ref={ref}
      />
      <div className="mt-2 flex flex-wrap gap-2 text-xs text-slate-600">
        {(locations.data ?? []).map((location) => (
          <span className="rounded-full border px-2 py-1" key={location.tripId}>
            {location.tripNumber}: {freshnessLabel(location.capturedAtUtc)}
          </span>
        ))}
      </div>
    </section>
  )
}

async function fetchBoard(request: Requester, filters: Filters) {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (typeof value === 'boolean') {
      if (value) query.set(key, 'true')
    } else if (value) {
      query.set(key, value)
    }
  }
  const response = await request(`/api/v1/operations/board?${query}`)
  if (!response.ok) throw new Error('Unable to load operations board.')
  return (await response.json()) as BoardResponse
}

async function fetchLocations(request: Requester) {
  const response = await request('/api/v1/operations/locations')
  if (!response.ok) throw new Error('Unable to load operations locations.')
  return (await response.json()) as LocationItem[]
}

function buildOptions(items: readonly BoardItem[]) {
  return {
    customerId: uniqueOptions(items, 'customerId', 'customerName'),
    branchId: uniqueOptions(items, 'branchId', 'branchName'),
    driverId: uniqueOptions(items, 'driverId', 'driverName'),
    vehicleId: uniqueOptions(items, 'vehicleId', 'vehicleReference'),
    commodityId: uniqueOptions(items, 'commodityId', 'commodityName'),
  }
}

function uniqueOptions(
  items: readonly BoardItem[],
  idKey: keyof BoardItem,
  labelKey: keyof BoardItem,
) {
  const map = new Map<string, string>()
  for (const item of items) {
    const id = item[idKey]
    const label = item[labelKey]
    if (typeof id === 'string' && typeof label === 'string') map.set(id, label)
  }
  return [...map.entries()]
    .map(([id, label]) => ({ id, label }))
    .sort((a, b) => a.label.localeCompare(b.label))
}

function freshnessColor(capturedAtUtc: string) {
  const minutes = (Date.now() - new Date(capturedAtUtc).getTime()) / 60_000
  return minutes > 30 ? '#b45309' : '#166534'
}

function freshnessLabel(capturedAtUtc: string) {
  const minutes = Math.max(
    0,
    Math.round((Date.now() - new Date(capturedAtUtc).getTime()) / 60_000),
  )
  return minutes > 30
    ? `${String(minutes)} min old, stale`
    : `${String(minutes)} min old`
}
