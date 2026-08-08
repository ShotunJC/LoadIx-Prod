import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Camera,
  CheckCircle2,
  LocateFixed,
  MapPinned,
  RefreshCw,
  Truck,
  WifiOff,
} from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { DocumentsPanel } from '@/features/documents/documents-panel'
import { getTrip, listTrips, type TripDetail } from '@/features/trips/trip-api'
import {
  createIndexedDbQueueStore,
  createOfflineQueue,
  isTransientStatus,
  type QueuedCommand,
} from '@/offline/command-queue'

const nextAction: Record<string, { route: string; label: string }> = {
  Dispatched: {
    route: 'collection-arrival',
    label: 'I have arrived at collection',
  },
  AtCollection: { route: 'start-loading', label: 'Start loading' },
  Loading: { route: 'complete-collection', label: 'Complete collection' },
  CollectionComplete: {
    route: 'depart-collection',
    label: 'Depart collection',
  },
  InTransit: { route: 'delivery-arrival', label: 'I have arrived at delivery' },
  AtDelivery: { route: 'start-unloading', label: 'Start unloading' },
  Unloading: { route: 'complete-delivery', label: 'Complete delivery' },
  DeliveryComplete: {
    route: 'submit-reconciliation',
    label: 'Submit for reconciliation',
  },
}

export function DriverPage() {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [selectedTripId, setSelectedTripId] = useState<string | null>(null)
  const [queued, setQueued] = useState<QueuedCommand[]>([])
  const [locationMessage, setLocationMessage] = useState<string | null>(null)
  const queue = useMemo(
    () => createOfflineQueue(createIndexedDbQueueStore()),
    [],
  )
  const trips = useQuery({
    queryKey: ['driver-trips'],
    queryFn: () =>
      listTrips(auth.request, { page: 1, search: '', status: 'all' }),
  })
  const selected = useQuery({
    queryKey: ['driver-trip', selectedTripId],
    enabled: Boolean(selectedTripId),
    queryFn: () => {
      if (!selectedTripId) throw new Error('Select a trip first.')
      return getTrip(auth.request, selectedTripId)
    },
  })
  const refreshQueue = async () => {
    setQueued(await createIndexedDbQueueStore().list())
  }
  const flush = async () => {
    if (!navigator.onLine) return
    await queue.flush(async (command) =>
      auth.request(command.request.url, {
        method: command.request.method,
        headers: {
          'Content-Type': 'application/json',
          'Idempotency-Key': command.idempotencyKey,
        },
        body: JSON.stringify(command.request.body),
      }),
    )
    await refreshQueue()
    await queryClient.invalidateQueries({ queryKey: ['driver-trips'] })
  }

  useEffect(() => {
    const queueTimer = window.setTimeout(() => {
      void refreshQueue()
    }, 0)
    const onOnline = () => {
      void flush()
    }
    window.addEventListener('online', onOnline)
    return () => {
      window.clearTimeout(queueTimer)
      window.removeEventListener('online', onOnline)
    }
  })

  const submit = async (
    kind: string,
    trip: TripDetail,
    url: string,
    body: Record<string, unknown>,
  ) => {
    const idempotencyKey = `driver-${crypto.randomUUID()}`
    if (!navigator.onLine) {
      await queue.enqueue(kind, { url, method: 'POST', body }, idempotencyKey)
      await refreshQueue()
      return
    }
    let response: Response
    try {
      response = await auth.request(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Idempotency-Key': idempotencyKey,
        },
        body: JSON.stringify(body),
      })
    } catch {
      await queue.enqueue(kind, { url, method: 'POST', body }, idempotencyKey)
      await refreshQueue()
      return
    }

    if (!response.ok) {
      if (response.status === 409) {
        setLocationMessage(
          'This trip changed elsewhere. Refresh it before retrying this update.',
        )
        await queryClient.invalidateQueries({
          queryKey: ['driver-trip', trip.id],
        })
      } else if (isTransientStatus(response.status)) {
        await queue.enqueue(kind, { url, method: 'POST', body }, idempotencyKey)
        await refreshQueue()
      } else {
        setLocationMessage(
          'The server rejected this update. Review the trip and retry manually.',
        )
      }
      return
    }

    await queryClient.invalidateQueries({ queryKey: ['driver-trips'] })
    await queryClient.invalidateQueries({
      queryKey: ['driver-trip', trip.id],
    })
  }

  const requestLocation = (trip: TripDetail) => {
    setLocationMessage(
      'LoadIx uses your location only to record this trip event. It does not start background tracking.',
    )
    navigator.geolocation.getCurrentPosition(
      (position) => {
        void submit('location', trip, `/api/v1/trips/${trip.id}/location`, {
          version: trip.version,
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMetres: position.coords.accuracy,
          capturedAtUtc: new Date(position.timestamp).toISOString(),
        })
      },
      () => {
        setLocationMessage(
          'Location permission was denied. You can still continue the trip workflow.',
        )
      },
      { enableHighAccuracy: false, maximumAge: 60_000, timeout: 12_000 },
    )
  }

  const next = useMemo(
    () =>
      trips.data?.items.find(
        (trip) => !['Completed', 'Cancelled', 'Rejected'].includes(trip.status),
      ),
    [trips.data],
  )

  if (!auth.user?.permissions.includes('Trip.Driver.Execute')) {
    return (
      <p className="p-6 text-sm text-slate-600">
        Driver access is required for this workspace.
      </p>
    )
  }

  return (
    <main className="mx-auto max-w-xl space-y-4 p-4 pb-24" id="main-content">
      <header className="rounded-2xl bg-slate-950 p-5 text-white">
        <p className="text-xs font-bold tracking-widest text-teal-300 uppercase">
          LoadIx Driver
        </p>
        <h1 className="mt-1 text-2xl font-black">Your work today</h1>
        <p className="mt-2 text-sm text-slate-300">
          Only trips assigned to you are shown.
        </p>
      </header>

      {!navigator.onLine ? (
        <div
          className="flex items-center gap-2 rounded-xl bg-amber-100 p-4 text-sm font-bold text-amber-950"
          role="status"
        >
          <WifiOff size={20} /> Offline: new updates will be queued safely.
        </div>
      ) : null}

      {queued.length ? (
        <section
          className="rounded-xl border border-amber-300 bg-amber-50 p-4"
          aria-label="Synchronization state"
        >
          <div className="flex items-center justify-between gap-3">
            <p className="font-bold text-amber-950">
              {queued.length} update{queued.length === 1 ? '' : 's'} waiting to
              sync
            </p>
            <Button
              onClick={() => void flush()}
              type="button"
              variant="outline"
            >
              <RefreshCw size={17} /> Retry
            </Button>
          </div>
          <ul className="mt-2 space-y-1 text-sm text-amber-900">
            {queued.map((item) => (
              <li key={item.id}>
                {item.kind}: {item.error ?? item.state}
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {next ? (
        <button
          className="w-full rounded-2xl border-2 border-teal-600 bg-teal-50 p-5 text-left"
          onClick={() => {
            setSelectedTripId(next.id)
          }}
          type="button"
        >
          <p className="text-xs font-bold text-teal-800 uppercase">Next trip</p>
          <p className="mt-1 text-xl font-black text-slate-950">
            {next.tripNumber}
          </p>
          <p className="mt-1 text-sm text-slate-700">
            {next.customerName} · {next.status}
          </p>
        </button>
      ) : (
        <p className="rounded-xl bg-white p-5 text-sm text-slate-600">
          No active trips are assigned to you.
        </p>
      )}

      <section aria-label="Assigned trips" className="space-y-2">
        <h2 className="px-1 text-lg font-black text-slate-950">
          Assigned trips
        </h2>
        {trips.data?.items.map((trip) => (
          <button
            className="w-full rounded-xl border bg-white p-4 text-left shadow-sm"
            key={trip.id}
            onClick={() => {
              setSelectedTripId(trip.id)
            }}
            type="button"
          >
            <p className="font-black text-slate-950">{trip.tripNumber}</p>
            <p className="text-sm text-slate-600">
              {trip.routeReference} · {trip.status}
            </p>
          </button>
        ))}
      </section>

      {selected.data ? (
        <DriverTripDetail
          trip={selected.data}
          onLocation={requestLocation}
          onSubmit={submit}
        />
      ) : null}
      {locationMessage ? (
        <p
          className="rounded-xl bg-sky-50 p-3 text-sm text-sky-900"
          role="status"
        >
          {locationMessage}
        </p>
      ) : null}
    </main>
  )
}

function DriverTripDetail({
  trip,
  onLocation,
  onSubmit,
}: {
  trip: TripDetail
  onLocation: (trip: TripDetail) => void
  onSubmit: (
    kind: string,
    trip: TripDetail,
    url: string,
    body: Record<string, unknown>,
  ) => Promise<void>
}) {
  const action = nextAction[trip.status]
  const [note, setNote] = useState('')
  const source = trip.status === 'Loading' && !trip.sourceWeighbridgeReference
  const destination =
    trip.status === 'Unloading' && !trip.destinationWeighbridgeReference
  return (
    <section
      className="space-y-4 rounded-2xl border bg-white p-5 shadow-sm"
      aria-label={`Trip ${trip.tripNumber}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-xl font-black">{trip.tripNumber}</h2>
          <p className="text-sm text-slate-600">
            {trip.status} · {trip.commodityName}
          </p>
        </div>
        <Truck className="text-teal-700" size={28} />
      </div>
      <div className="grid gap-2 sm:grid-cols-2">
        <Directions label="Collection" location={trip.sourceLocationName} />
        <Directions label="Delivery" location={trip.destinationLocationName} />
      </div>
      <Button
        className="min-h-14 w-full text-base"
        onClick={() => {
          onLocation(trip)
        }}
        type="button"
        variant="outline"
      >
        <LocateFixed size={20} /> Share location for this trip event
      </Button>
      {source || destination ? (
        <WeighbridgeCapture trip={trip} source={source} onSubmit={onSubmit} />
      ) : null}
      {action && !source && !destination ? (
        <Button
          className="min-h-16 w-full text-base"
          onClick={() =>
            void onSubmit(
              'trip stage',
              trip,
              `/api/v1/trips/${trip.id}/${action.route}`,
              { version: trip.version },
            )
          }
          type="button"
        >
          <CheckCircle2 size={21} /> {action.label}
        </Button>
      ) : null}
      <form
        className="space-y-2 border-t pt-4"
        onSubmit={(event) => {
          event.preventDefault()
          if (!note.trim()) return
          void onSubmit('trip note', trip, `/api/v1/trips/${trip.id}/notes`, {
            version: trip.version,
            note: note.trim(),
          }).then(() => {
            setNote('')
          })
        }}
      >
        <label className="block font-bold" htmlFor={`trip-note-${trip.id}`}>
          Add a trip note
        </label>
        <textarea
          className="min-h-24 w-full rounded-lg border p-3"
          id={`trip-note-${trip.id}`}
          onChange={(event) => {
            setNote(event.target.value)
          }}
          placeholder="Optional operational note"
          value={note}
        />
        <Button className="min-h-12" type="submit" variant="outline">
          Save note
        </Button>
      </form>
      <div className="border-t pt-4">
        <p className="mb-2 flex items-center gap-2 font-bold">
          <Camera size={18} /> Tickets and proof of delivery
        </p>
        <DocumentsPanel tripId={trip.id} tripStatus={trip.status} />
      </div>
    </section>
  )
}

function Directions({ label, location }: { label: string; location: string }) {
  return (
    <a
      className="flex min-h-14 items-center gap-3 rounded-xl border p-3 font-bold text-teal-800 underline"
      href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(location)}`}
      rel="noreferrer"
      target="_blank"
    >
      <MapPinned size={20} /> {label}
    </a>
  )
}

function WeighbridgeCapture({
  trip,
  source,
  onSubmit,
}: {
  trip: TripDetail
  source: boolean
  onSubmit: (
    kind: string,
    trip: TripDetail,
    url: string,
    body: Record<string, unknown>,
  ) => Promise<void>
}) {
  return (
    <form
      className="space-y-3 rounded-xl bg-slate-50 p-4"
      onSubmit={(event) => {
        event.preventDefault()
        const values = new FormData(event.currentTarget)
        void onSubmit(
          'weighbridge',
          trip,
          `/api/v1/trips/${trip.id}/${source ? 'source' : 'destination'}-weighbridge`,
          {
            version: trip.version,
            grossWeight: Number(values.get('gross')),
            tareWeight: Number(values.get('tare')),
            reference: values.get('reference'),
          },
        )
      }}
    >
      <h3 className="font-black">
        {source ? 'Collection' : 'Delivery'} weighbridge
      </h3>
      <label className="block font-bold">
        Gross weight
        <input
          className="mt-1 min-h-14 w-full rounded-lg border p-3"
          min="0.001"
          name="gross"
          required
          step="0.001"
          type="number"
        />
      </label>
      <label className="block font-bold">
        Tare weight
        <input
          className="mt-1 min-h-14 w-full rounded-lg border p-3"
          min="0"
          name="tare"
          required
          step="0.001"
          type="number"
        />
      </label>
      <label className="block font-bold">
        Ticket reference
        <input
          className="mt-1 min-h-14 w-full rounded-lg border p-3"
          name="reference"
          required
        />
      </label>
      <Button className="min-h-14 w-full" type="submit">
        Save weighbridge details
      </Button>
    </form>
  )
}
