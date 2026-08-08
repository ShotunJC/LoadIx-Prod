import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle,
  ArrowRight,
  CalendarClock,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleDot,
  ClipboardList,
  Truck,
  UserRound,
  X,
} from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { DocumentsPanel } from '@/features/documents/documents-panel'
import { TripFinancialPanel } from '@/features/finance/trip-financial-panel'
import { listMasterData } from '@/features/master-data/master-data-api'
import type { MasterDataItem } from '@/features/master-data/master-data-api'
import {
  addTripNote,
  assignTrip,
  executeTripCommand,
  getTrip,
  listTrips,
  recordTripWeighbridge,
} from '@/features/trips/trip-api'
import type { TripDetail } from '@/features/trips/trip-api'

const statuses = [
  'all',
  'Planned',
  'Assigned',
  'Dispatched',
  'AtCollection',
  'Loading',
  'CollectionComplete',
  'InTransit',
  'AtDelivery',
  'Unloading',
  'DeliveryComplete',
  'ReconciliationRequired',
  'Completed',
  'Cancelled',
  'Rejected',
]

const nextAction: Record<string, { route: string; label: string }> = {
  Assigned: { route: 'dispatch', label: 'Dispatch trip' },
  Dispatched: {
    route: 'collection-arrival',
    label: 'Confirm collection arrival',
  },
  AtCollection: { route: 'start-loading', label: 'Start loading' },
  Loading: { route: 'complete-collection', label: 'Complete collection' },
  CollectionComplete: {
    route: 'depart-collection',
    label: 'Depart collection',
  },
  InTransit: {
    route: 'delivery-arrival',
    label: 'Confirm delivery arrival',
  },
  AtDelivery: { route: 'start-unloading', label: 'Start unloading' },
  Unloading: { route: 'complete-delivery', label: 'Complete delivery' },
  DeliveryComplete: {
    route: 'submit-reconciliation',
    label: 'Submit for reconciliation',
  },
  ReconciliationRequired: { route: 'complete', label: 'Complete trip' },
}

function field(item: MasterDataItem, name: string) {
  const value = item[name]
  return typeof value === 'string' ? value : ''
}

function displayDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function TripStatusBadge({ status }: { status: string }) {
  const exception = [
    'Rejected',
    'Cancelled',
    'ReconciliationRequired',
  ].includes(status)
  const complete = status === 'Completed'
  return (
    <span
      className={`inline-flex rounded-full px-2.5 py-1 text-xs font-bold ${
        exception
          ? 'bg-amber-100 text-amber-900'
          : complete
            ? 'bg-emerald-100 text-emerald-800'
            : 'bg-sky-100 text-sky-800'
      }`}
    >
      {status}
    </span>
  )
}

export function TripsPage() {
  const auth = useAuth()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [selectedId, setSelectedId] = useState<string>()
  const query = useQuery({
    queryKey: ['trips', page, search, status],
    queryFn: () => listTrips(auth.request, { page, search, status }),
  })

  return (
    <section className="space-y-5" aria-labelledby="trips-title">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-brand-700 text-xs font-bold tracking-[0.16em] uppercase">
            Dispatch control
          </p>
          <h1
            className="mt-1 text-3xl font-black tracking-tight text-slate-950"
            id="trips-title"
          >
            Trips
          </h1>
          <p className="mt-1 text-sm text-slate-600">
            Assign eligible resources and control every operational transition.
          </p>
        </div>
        <div className="rounded-xl border bg-white px-4 py-3 text-right shadow-sm">
          <p className="text-xs font-semibold text-slate-500">Visible trips</p>
          <p className="text-2xl font-black text-slate-950">
            {query.data?.totalCount ?? '—'}
          </p>
        </div>
      </header>

      <div className="grid gap-3 rounded-xl border bg-white p-4 shadow-sm sm:grid-cols-[1fr_14rem]">
        <label className="text-sm font-semibold text-slate-700">
          Search trips
          <input
            className="mt-1 block min-h-11 w-full rounded-md border px-3 font-normal"
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
            placeholder="Trip, contract, or route"
            value={search}
          />
        </label>
        <label className="text-sm font-semibold text-slate-700">
          Status
          <select
            className="mt-1 block min-h-11 w-full rounded-md border px-3 font-normal"
            onChange={(event) => {
              setStatus(event.target.value)
              setPage(1)
            }}
            value={status}
          >
            {statuses.map((item) => (
              <option key={item} value={item}>
                {item === 'all' ? 'All statuses' : item}
              </option>
            ))}
          </select>
        </label>
      </div>

      {query.isPending ? (
        <div className="grid gap-3" aria-label="Loading trips">
          {[1, 2, 3].map((item) => (
            <div
              className="h-24 animate-pulse rounded-xl bg-slate-200"
              key={item}
            />
          ))}
        </div>
      ) : query.isError ? (
        <div className="rounded-xl border border-red-200 bg-red-50 p-5 text-red-800">
          <p className="font-bold">Trips could not be loaded</p>
          <p className="text-sm">{query.error.message}</p>
        </div>
      ) : query.data.items.length === 0 ? (
        <div className="rounded-xl border border-dashed bg-white p-10 text-center">
          <ClipboardList
            className="mx-auto text-slate-400"
            aria-hidden="true"
          />
          <p className="mt-3 font-bold text-slate-900">No matching trips</p>
          <p className="text-sm text-slate-500">
            Generate trips from a planned load instruction to begin dispatch.
          </p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border bg-white shadow-sm">
          <div className="divide-y">
            {query.data.items.map((trip) => (
              <button
                className="grid w-full gap-3 p-4 text-left hover:bg-slate-50 focus-visible:bg-slate-50 sm:grid-cols-[1.2fr_1fr_1fr_auto]"
                key={trip.id}
                onClick={() => {
                  setSelectedId(trip.id)
                }}
                type="button"
              >
                <span>
                  <span className="block font-black text-slate-950">
                    {trip.tripNumber}
                  </span>
                  <span className="text-xs text-slate-500">
                    {trip.customerName} · {trip.instructionNumber}
                  </span>
                </span>
                <span className="text-sm">
                  <span className="block font-semibold text-slate-800">
                    {trip.routeReference}
                  </span>
                  <span className="text-xs text-slate-500">
                    {trip.plannedQuantity} {trip.quantityUnit}
                  </span>
                </span>
                <span className="text-sm">
                  <span className="block font-semibold text-slate-800">
                    {trip.driverName ?? 'Unassigned'}
                  </span>
                  <span className="text-xs text-slate-500">
                    {trip.vehicleReference ?? 'No vehicle'}
                  </span>
                </span>
                <span className="flex items-center gap-2">
                  {trip.hasException ? (
                    <AlertTriangle
                      aria-label="Exception"
                      className="text-amber-600"
                      size={18}
                    />
                  ) : null}
                  <TripStatusBadge status={trip.status} />
                  <ArrowRight aria-hidden="true" size={17} />
                </span>
              </button>
            ))}
          </div>
          <footer className="flex items-center justify-between border-t bg-slate-50 px-4 py-3 text-sm">
            <span>
              Page {query.data.page} of {Math.max(query.data.totalPages, 1)}
            </span>
            <div className="flex gap-2">
              <Button
                aria-label="Previous page"
                disabled={page === 1}
                onClick={() => {
                  setPage((value) => value - 1)
                }}
                type="button"
                variant="outline"
              >
                <ChevronLeft size={17} />
              </Button>
              <Button
                aria-label="Next page"
                disabled={page >= query.data.totalPages}
                onClick={() => {
                  setPage((value) => value + 1)
                }}
                type="button"
                variant="outline"
              >
                <ChevronRight size={17} />
              </Button>
            </div>
          </footer>
        </div>
      )}

      {selectedId ? (
        <TripDetailPanel
          id={selectedId}
          onClose={() => {
            setSelectedId(undefined)
          }}
        />
      ) : null}
    </section>
  )
}

function TripDetailPanel({ id, onClose }: { id: string; onClose: () => void }) {
  const auth = useAuth()
  const client = useQueryClient()
  const detail = useQuery({
    queryKey: ['trip', id],
    queryFn: () => getTrip(auth.request, id),
  })
  const update = useMutation({
    mutationFn: async (
      operation: (trip: TripDetail) => Promise<TripDetail>,
    ) => {
      if (!detail.data) throw new Error('Trip details are not ready.')
      return operation(detail.data)
    },
    onSuccess: (trip) => {
      client.setQueryData(['trip', id], trip)
      void client.invalidateQueries({ queryKey: ['trips'] })
    },
  })
  const canManage = auth.user?.permissions.includes('Trip.Manage') ?? false
  const canDispatch = auth.user?.permissions.includes('Trip.Dispatch') ?? false
  const canComplete = auth.user?.permissions.includes('Trip.Complete') ?? false
  const canOverride = auth.user?.permissions.includes('Trip.Override') ?? false
  const canAssign =
    detail.data !== undefined &&
    canManage &&
    (['Planned', 'Assigned'].includes(detail.data.status) ||
      (canOverride &&
        !['Completed', 'Cancelled', 'Rejected'].includes(detail.data.status)))

  return (
    <div
      aria-labelledby="trip-detail-title"
      aria-modal="true"
      className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/50 p-3 sm:p-6"
      role="dialog"
    >
      <div className="ml-auto min-h-full w-full max-w-4xl rounded-xl bg-slate-50 shadow-2xl">
        <header className="sticky top-0 z-10 flex items-center justify-between border-b bg-white px-5 py-4">
          <div>
            <p className="text-xs font-bold tracking-wider text-slate-500 uppercase">
              Operational trip
            </p>
            <h2
              className="text-xl font-black text-slate-950"
              id="trip-detail-title"
            >
              {detail.data?.tripNumber ?? 'Loading trip'}
            </h2>
          </div>
          <Button aria-label="Close trip" onClick={onClose} variant="ghost">
            <X size={20} />
          </Button>
        </header>

        {detail.isPending ? (
          <div className="p-8 text-sm text-slate-600">
            Loading trip details…
          </div>
        ) : detail.isError ? (
          <div className="m-5 rounded-lg bg-red-50 p-4 text-red-800">
            {detail.error.message}
          </div>
        ) : (
          <div className="space-y-5 p-5">
            <TripSummary trip={detail.data} />

            {auth.user?.permissions.some((permission) =>
              ['Document.View', 'Document.Driver'].includes(permission),
            ) ? (
              <DocumentsPanel
                tripId={detail.data.id}
                tripStatus={detail.data.status}
              />
            ) : null}

            {auth.user?.permissions.includes('Finance.View') ? (
              <TripFinancialPanel tripId={detail.data.id} />
            ) : null}

            {update.isError ? (
              <div
                className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800"
                role="alert"
              >
                {update.error.message}
              </div>
            ) : null}

            {canAssign ? (
              <AssignmentForm
                busy={update.isPending}
                reassign={detail.data.status !== 'Planned'}
                onAssign={(values) =>
                  update.mutateAsync((trip) =>
                    assignTrip(auth.request, trip, values),
                  )
                }
              />
            ) : null}

            <LifecycleControls
              busy={update.isPending}
              canComplete={canComplete}
              canDispatch={canDispatch}
              canManage={canManage}
              onCommand={(route, reason) =>
                update.mutateAsync((trip) =>
                  executeTripCommand(auth.request, trip, route, reason),
                )
              }
              onWeighbridge={(source, values) =>
                update.mutateAsync((trip) =>
                  recordTripWeighbridge(auth.request, trip, source, values),
                )
              }
              trip={detail.data}
            />

            {canManage ? (
              <NoteForm
                busy={update.isPending}
                onSave={(note) =>
                  update.mutateAsync((trip) =>
                    addTripNote(auth.request, trip, note),
                  )
                }
              />
            ) : null}

            <Timeline trip={detail.data} />
          </div>
        )}
      </div>
    </div>
  )
}

function TripSummary({ trip }: { trip: TripDetail }) {
  return (
    <div className="grid gap-4 rounded-xl border bg-white p-5 shadow-sm sm:grid-cols-2 lg:grid-cols-4">
      <div>
        <p className="text-xs font-semibold text-slate-500">Status</p>
        <div className="mt-1">
          <TripStatusBadge status={trip.status} />
        </div>
      </div>
      <div>
        <p className="text-xs font-semibold text-slate-500">Customer</p>
        <p className="font-bold text-slate-900">{trip.customerName}</p>
      </div>
      <div>
        <p className="text-xs font-semibold text-slate-500">Route</p>
        <p className="font-bold text-slate-900">{trip.routeReference}</p>
        <p className="text-xs text-slate-500">
          {trip.sourceLocationName} → {trip.destinationLocationName}
        </p>
      </div>
      <div>
        <p className="text-xs font-semibold text-slate-500">Planned quantity</p>
        <p className="font-bold text-slate-900">
          {trip.plannedQuantity} {trip.quantityUnit}
        </p>
      </div>
      <div className="sm:col-span-2">
        <p className="flex items-center gap-1 text-xs font-semibold text-slate-500">
          <CalendarClock size={14} /> Planned window
        </p>
        <p className="text-sm font-semibold text-slate-800">
          {displayDate(trip.plannedCollectionAtUtc)} →{' '}
          {displayDate(trip.plannedDeliveryAtUtc)}
        </p>
      </div>
      <div>
        <p className="flex items-center gap-1 text-xs font-semibold text-slate-500">
          <UserRound size={14} /> Driver
        </p>
        <p className="text-sm font-bold text-slate-900">
          {trip.driverName ?? 'Not assigned'}
        </p>
      </div>
      <div>
        <p className="flex items-center gap-1 text-xs font-semibold text-slate-500">
          <Truck size={14} /> Equipment
        </p>
        <p className="text-sm font-bold text-slate-900">
          {trip.vehicleReference ?? 'Not assigned'}
        </p>
        <p className="text-xs text-slate-500">
          {trip.trailerReferences.join(', ') || 'No trailers'}
        </p>
      </div>
      <div className="sm:col-span-2 lg:col-span-4">
        <p className="text-xs font-semibold text-slate-500">
          Contract and immutable rate
        </p>
        <p className="text-sm text-slate-800">
          {trip.contractNumber} · {trip.rateSnapshot.rateType} ·{' '}
          {trip.rateSnapshot.currencyCode}
        </p>
      </div>
      {trip.cancellationOrRejectionReason ? (
        <div className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900 sm:col-span-2 lg:col-span-4">
          <strong>Exception:</strong> {trip.cancellationOrRejectionReason}
        </div>
      ) : null}
    </div>
  )
}

function AssignmentForm({
  busy,
  onAssign,
  reassign,
}: {
  busy: boolean
  onAssign: (values: {
    driverId: string
    vehicleId: string
    trailerIds: string[]
    reason?: string
  }) => Promise<unknown>
  reassign: boolean
}) {
  const auth = useAuth()
  const resources = useQuery({
    queryKey: ['trip-assignment-options'],
    queryFn: async () => {
      const common = {
        page: 1,
        pageSize: 100,
        search: '',
        status: 'active',
        direction: 'asc',
      }
      const [drivers, vehicles, trailers] = await Promise.all([
        listMasterData(auth.request, 'drivers', {
          ...common,
          sort: 'reference',
        }),
        listMasterData(auth.request, 'vehicles', {
          ...common,
          sort: 'reference',
        }),
        listMasterData(auth.request, 'trailers', {
          ...common,
          sort: 'reference',
        }),
      ])
      return { drivers, vehicles, trailers }
    },
  })
  const [driverId, setDriverId] = useState('')
  const [vehicleId, setVehicleId] = useState('')
  const [trailerIds, setTrailerIds] = useState<string[]>([])
  const [reason, setReason] = useState('')

  return (
    <form
      className="space-y-4 rounded-xl border bg-white p-5 shadow-sm"
      onSubmit={(event) => {
        event.preventDefault()
        void onAssign({
          driverId,
          vehicleId,
          trailerIds,
          ...(reassign ? { reason } : {}),
        })
      }}
    >
      <div>
        <h3 className="font-black text-slate-950">
          {reassign ? 'Reassign resources' : 'Assign resources'}
        </h3>
        <p className="text-sm text-slate-500">
          Active, compliant resources are checked for overlapping work.
        </p>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-sm font-semibold">
          Driver
          <select
            className="mt-1 min-h-11 w-full rounded-md border px-3 font-normal"
            onChange={(event) => {
              setDriverId(event.target.value)
            }}
            required
            value={driverId}
          >
            <option value="">Select driver</option>
            {resources.data?.drivers.items.map((item) => (
              <option key={item.id} value={item.id}>
                {field(item, 'firstName')} {field(item, 'lastName')} ·{' '}
                {item.reference}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm font-semibold">
          Vehicle
          <select
            className="mt-1 min-h-11 w-full rounded-md border px-3 font-normal"
            onChange={(event) => {
              setVehicleId(event.target.value)
            }}
            required
            value={vehicleId}
          >
            <option value="">Select vehicle</option>
            {resources.data?.vehicles.items.map((item) => (
              <option key={item.id} value={item.id}>
                {item.reference} · {field(item, 'registration')}
              </option>
            ))}
          </select>
        </label>
      </div>
      <fieldset>
        <legend className="text-sm font-semibold">Trailers (up to two)</legend>
        <div className="mt-2 grid gap-2 sm:grid-cols-3">
          {resources.data?.trailers.items.map((item) => (
            <label
              className="flex min-h-11 items-center gap-2 rounded-md border px-3 text-sm"
              key={item.id}
            >
              <input
                checked={trailerIds.includes(item.id)}
                disabled={
                  !trailerIds.includes(item.id) && trailerIds.length >= 2
                }
                onChange={(event) => {
                  setTrailerIds((values) =>
                    event.target.checked
                      ? [...values, item.id]
                      : values.filter((value) => value !== item.id),
                  )
                }}
                type="checkbox"
              />
              {item.reference}
            </label>
          ))}
        </div>
      </fieldset>
      {reassign ? (
        <label className="block text-sm font-semibold">
          Reassignment reason
          <textarea
            className="mt-1 min-h-24 w-full rounded-md border px-3 py-2 font-normal"
            onChange={(event) => {
              setReason(event.target.value)
            }}
            required
            value={reason}
          />
        </label>
      ) : null}
      <Button disabled={busy || resources.isPending} type="submit">
        {busy
          ? 'Checking resources…'
          : reassign
            ? 'Reassign trip'
            : 'Assign trip'}
      </Button>
    </form>
  )
}

function LifecycleControls({
  trip,
  busy,
  canManage,
  canDispatch,
  canComplete,
  onCommand,
  onWeighbridge,
}: {
  trip: TripDetail
  busy: boolean
  canManage: boolean
  canDispatch: boolean
  canComplete: boolean
  onCommand: (route: string, reason?: string) => Promise<unknown>
  onWeighbridge: (
    source: boolean,
    values: { grossWeight: number; tareWeight: number; reference: string },
  ) => Promise<unknown>
}) {
  const action = nextAction[trip.status]
  const allowed =
    action &&
    (action.route === 'dispatch'
      ? canDispatch
      : action.route === 'complete'
        ? canComplete
        : canManage)
  const [reason, setReason] = useState('')
  const sourceRequired =
    trip.status === 'Loading' &&
    trip.sourceWeightCaptureRequired &&
    !trip.sourceWeighbridgeReference
  const destinationRequired =
    trip.status === 'Unloading' &&
    trip.destinationWeightCaptureRequired &&
    !trip.destinationWeighbridgeReference

  return (
    <div className="space-y-4 rounded-xl border bg-white p-5 shadow-sm">
      <div>
        <h3 className="font-black text-slate-950">Lifecycle controls</h3>
        <p className="text-sm text-slate-500">
          Only the next legal state is available. The API enforces the same
          rules.
        </p>
      </div>
      {sourceRequired || destinationRequired ? (
        <WeighbridgeForm
          busy={busy}
          label={
            sourceRequired ? 'Source weighbridge' : 'Destination weighbridge'
          }
          onSave={(values) => onWeighbridge(sourceRequired, values)}
        />
      ) : null}
      {trip.status === 'ReconciliationRequired' && canComplete ? (
        <div className="flex flex-wrap gap-2">
          <p className="w-full text-sm text-slate-600">
            Reconciliation:{' '}
            {trip.reconciliationStatus === 'NotApplicable'
              ? 'Not applicable â€” this route does not use weighbridges.'
              : trip.reconciliationStatus.replace(
                  /([a-z])([A-Z])/g,
                  '$1 $2',
                )}{' '}
            Quantity source:{' '}
            {trip.quantitySource.replace(/([a-z])([A-Z])/g, '$1 $2')}.
          </p>
          <Button
            disabled={busy || trip.isReconciled}
            onClick={() => {
              void onCommand('confirm-reconciliation')
            }}
            type="button"
            variant="outline"
          >
            {trip.isReconciled ? 'Reconciled' : 'Confirm reconciliation'}
          </Button>
          <Button
            disabled={busy || trip.hasRequiredDocuments}
            onClick={() => {
              void onCommand('confirm-required-documents')
            }}
            type="button"
            variant="outline"
          >
            {trip.hasRequiredDocuments
              ? 'Documents confirmed'
              : 'Confirm required documents'}
          </Button>
        </div>
      ) : null}
      {allowed && !sourceRequired && !destinationRequired ? (
        <Button
          disabled={
            busy ||
            (action.route === 'complete' &&
              (!trip.isReconciled || !trip.hasRequiredDocuments))
          }
          onClick={() => {
            void onCommand(action.route)
          }}
          type="button"
        >
          {action.label}
        </Button>
      ) : null}
      {canManage &&
      !['Completed', 'Cancelled', 'Rejected'].includes(trip.status) ? (
        <div className="grid gap-2 border-t pt-4 sm:grid-cols-[1fr_auto_auto]">
          <label className="text-sm font-semibold">
            Exception reason
            <input
              className="mt-1 min-h-11 w-full rounded-md border px-3 font-normal"
              onChange={(event) => {
                setReason(event.target.value)
              }}
              placeholder="Required for cancellation or rejection"
              value={reason}
            />
          </label>
          <Button
            className="self-end"
            disabled={busy || !reason.trim()}
            onClick={() => {
              void onCommand('cancel', reason)
            }}
            type="button"
            variant="outline"
          >
            Cancel
          </Button>
          <Button
            className="self-end"
            disabled={busy || !reason.trim()}
            onClick={() => {
              void onCommand('reject', reason)
            }}
            type="button"
            variant="outline"
          >
            Reject
          </Button>
        </div>
      ) : null}
    </div>
  )
}

function WeighbridgeForm({
  label,
  busy,
  onSave,
}: {
  label: string
  busy: boolean
  onSave: (values: {
    grossWeight: number
    tareWeight: number
    reference: string
  }) => Promise<unknown>
}) {
  return (
    <form
      className="grid gap-3 rounded-lg bg-slate-50 p-4 sm:grid-cols-3"
      onSubmit={(event) => {
        event.preventDefault()
        const data = new FormData(event.currentTarget)
        const reference = data.get('reference')
        if (typeof reference !== 'string') return
        void onSave({
          grossWeight: Number(data.get('grossWeight')),
          tareWeight: Number(data.get('tareWeight')),
          reference,
        })
      }}
    >
      <h4 className="font-bold text-slate-900 sm:col-span-3">{label}</h4>
      <label className="text-sm font-semibold">
        Gross weight
        <input
          className="mt-1 min-h-11 w-full rounded-md border px-3"
          min="0.001"
          name="grossWeight"
          required
          step="0.001"
          type="number"
        />
      </label>
      <label className="text-sm font-semibold">
        Tare weight
        <input
          className="mt-1 min-h-11 w-full rounded-md border px-3"
          min="0"
          name="tareWeight"
          required
          step="0.001"
          type="number"
        />
      </label>
      <label className="text-sm font-semibold">
        Ticket reference
        <input
          className="mt-1 min-h-11 w-full rounded-md border px-3"
          name="reference"
          required
        />
      </label>
      <Button className="sm:col-span-3 sm:w-fit" disabled={busy} type="submit">
        Record weighbridge
      </Button>
    </form>
  )
}

function NoteForm({
  busy,
  onSave,
}: {
  busy: boolean
  onSave: (note: string) => Promise<unknown>
}) {
  const [note, setNote] = useState('')
  return (
    <form
      className="rounded-xl border bg-white p-5 shadow-sm"
      onSubmit={(event) => {
        event.preventDefault()
        void onSave(note).then(() => {
          setNote('')
        })
      }}
    >
      <label className="text-sm font-bold text-slate-900">
        Add operational note
        <textarea
          className="mt-2 block min-h-24 w-full rounded-md border p-3 font-normal"
          onChange={(event) => {
            setNote(event.target.value)
          }}
          required
          value={note}
        />
      </label>
      <Button className="mt-3" disabled={busy || !note.trim()} type="submit">
        Add note
      </Button>
    </form>
  )
}

function Timeline({ trip }: { trip: TripDetail }) {
  return (
    <div className="rounded-xl border bg-white p-5 shadow-sm">
      <h3 className="font-black text-slate-950">Timeline and history</h3>
      {trip.transitions.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">
          No lifecycle transitions have been recorded yet.
        </p>
      ) : (
        <ol className="mt-4 space-y-4">
          {trip.transitions.map((item) => (
            <li className="grid grid-cols-[1.5rem_1fr] gap-3" key={item.id}>
              {item.isManualOverride ? (
                <AlertTriangle className="text-amber-600" size={19} />
              ) : item.newStatus === 'Completed' ? (
                <CheckCircle2 className="text-emerald-600" size={19} />
              ) : (
                <CircleDot className="text-brand-600" size={19} />
              )}
              <div>
                <p className="font-bold text-slate-900">
                  {item.previousStatus} → {item.newStatus}
                </p>
                <p className="text-xs text-slate-500">
                  {displayDate(item.occurredAtUtc)}
                  {item.isManualOverride ? ' · Manual override' : ''}
                </p>
                {item.reason ? (
                  <p className="mt-1 text-sm text-slate-700">{item.reason}</p>
                ) : null}
              </div>
            </li>
          ))}
        </ol>
      )}
      {trip.operationalNotes ? (
        <div className="mt-5 border-t pt-4">
          <p className="text-xs font-semibold text-slate-500 uppercase">
            Operational notes
          </p>
          <p className="mt-1 text-sm whitespace-pre-wrap text-slate-800">
            {trip.operationalNotes}
          </p>
        </div>
      ) : null}
    </div>
  )
}
