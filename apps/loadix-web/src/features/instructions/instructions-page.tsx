import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { ChevronLeft, Pencil, Plus, X } from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { FieldLabel } from '@/components/ui/field-help'
import { getContract, listContracts } from '@/features/contracts/contract-api'
import {
  cancelInstruction,
  generateTrips,
  getInstruction,
  getInstructionHistory,
  listInstructions,
  planInstruction,
  saveInstruction,
  type InstructionDetail,
} from './instruction-api'

interface FormValues {
  instructionNumber: string
  transportContractId: string
  contractRouteId: string
  commodityId: string
  requestedQuantity: string
  quantityUnit: string
  requestedTripCount: string
  collectionWindowStart: string
  collectionWindowEnd: string
  deliveryWindowStart: string
  deliveryWindowEnd: string
  customerPurchaseOrderReference: string
  customerInstructionReference: string
  priority: string
  specialInstructions: string
}

const instructionFieldHelp: Record<keyof FormValues, string> = {
  instructionNumber:
    'The unique load or order reference used by operations and the customer.',
  transportContractId:
    'The active agreement that supplies the customer, route, commodity and billing rules.',
  contractRouteId: 'The approved collection-to-delivery route for this load.',
  commodityId: 'The material or goods that will be transported.',
  requestedQuantity: 'The total quantity the customer has asked to move.',
  quantityUnit: 'The unit used for the requested quantity and trip planning.',
  requestedTripCount:
    'An optional estimate of how many trips operations expects to use.',
  collectionWindowStart:
    'The earliest date and time the first collection may begin.',
  collectionWindowEnd:
    'The latest date and time by which collection should occur.',
  deliveryWindowStart:
    'The earliest date and time the delivery site will accept the load.',
  deliveryWindowEnd: 'The latest date and time by which delivery should occur.',
  customerPurchaseOrderReference:
    'The customer purchase-order number that should follow this work into billing.',
  customerInstructionReference:
    'Any separate instruction, call-off or booking reference supplied by the customer.',
  priority:
    'Signals how urgently dispatch should schedule and monitor this load.',
  specialInstructions:
    'Load-specific safety, handling, access or document requirements for operations.',
}

const tomorrow = new Date(Date.now() + 86_400_000)
const dateTime = (date: Date) => date.toISOString().slice(0, 16)

export function InstructionsPage() {
  const auth = useAuth()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [priority, setPriority] = useState('all')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [editor, setEditor] = useState<InstructionDetail | 'new' | null>(null)
  const instructions = useQuery({
    queryKey: ['load-instructions', page, search, status, priority],
    queryFn: () =>
      listInstructions(auth.request, { page, search, status, priority }),
  })
  const detail = useQuery({
    queryKey: ['load-instruction', selectedId],
    queryFn: () =>
      selectedId
        ? getInstruction(auth.request, selectedId)
        : Promise.reject(new Error('An instruction must be selected.')),
    enabled: selectedId !== null,
  })
  const canManage = auth.user?.permissions.includes('Trip.Manage') === true

  if (!auth.user?.permissions.includes('Trip.View')) {
    return (
      <Message title="Access denied">
        You cannot view load instructions.
      </Message>
    )
  }
  if (selectedId) {
    return (
      <InstructionDetailView
        canManage={canManage}
        error={detail.error}
        instruction={detail.data}
        loading={detail.isLoading}
        onBack={() => {
          setSelectedId(null)
        }}
        onEdit={(instruction) => {
          setEditor(instruction)
        }}
      >
        {editor ? (
          <InstructionWizard
            existing={editor === 'new' ? undefined : editor}
            onClose={() => {
              setEditor(null)
            }}
            onSaved={(saved) => {
              setEditor(null)
              setSelectedId(saved.id)
            }}
          />
        ) : null}
      </InstructionDetailView>
    )
  }

  return (
    <section>
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-bold tracking-[0.16em] text-emerald-700 uppercase">
            Planning
          </p>
          <h1 className="mt-1 text-3xl font-black text-slate-950">
            Load instructions
          </h1>
          <p className="mt-1 text-slate-600">
            Customer transport requests, planned quantities and trip progress.
          </p>
        </div>
        {canManage ? (
          <Button
            onClick={() => {
              setEditor('new')
            }}
            type="button"
          >
            <Plus aria-hidden="true" size={18} /> New instruction
          </Button>
        ) : null}
      </div>
      <div className="mb-4 flex flex-wrap gap-3 rounded-xl border bg-white p-4">
        <label>
          <span className="sr-only">Search load instructions</span>
          <input
            className="h-11 min-w-64 rounded-md border px-3"
            onChange={(event) => {
              setPage(1)
              setSearch(event.target.value)
            }}
            placeholder="Search number or customer reference"
            type="search"
            value={search}
          />
        </label>
        <Filter
          label="Instruction status"
          onChange={(value) => {
            setPage(1)
            setStatus(value)
          }}
          options={[
            'all',
            'Draft',
            'Planned',
            'InProgress',
            'Completed',
            'Cancelled',
          ]}
          value={status}
        />
        <Filter
          label="Priority filter"
          onChange={(value) => {
            setPage(1)
            setPriority(value)
          }}
          options={['all', 'Low', 'Normal', 'High', 'Urgent']}
          value={priority}
        />
      </div>
      {instructions.isError ? (
        <Message title="Instructions could not be loaded">
          {instructions.error.message}
        </Message>
      ) : instructions.isLoading ? (
        <div className="h-72 animate-pulse rounded-xl bg-slate-200" />
      ) : instructions.data?.items.length === 0 ? (
        <Message title="No load instructions">
          Create an instruction or change the current filters.
        </Message>
      ) : (
        <div className="overflow-x-auto rounded-xl border bg-white">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-50 text-xs uppercase">
              <tr>
                <th className="p-4">Instruction</th>
                <th className="p-4">Customer / contract</th>
                <th className="p-4">Quantity</th>
                <th className="p-4">Collection</th>
                <th className="p-4">Priority</th>
                <th className="p-4">Status</th>
              </tr>
            </thead>
            <tbody>
              {instructions.data?.items.map((item) => (
                <tr className="border-t hover:bg-slate-50" key={item.id}>
                  <td className="p-4">
                    <button
                      className="font-black text-emerald-800 hover:underline"
                      onClick={() => {
                        setSelectedId(item.id)
                      }}
                      type="button"
                    >
                      {item.instructionNumber}
                    </button>
                    <span className="block text-xs text-slate-500">
                      {item.commodityName} · {item.generatedTripCount}{' '}
                      {item.generatedTripCount === 1 ? 'trip' : 'trips'}
                    </span>
                  </td>
                  <td className="p-4">
                    {item.customerName}
                    <span className="block text-xs text-slate-500">
                      {item.contractNumber}
                    </span>
                  </td>
                  <td className="p-4">
                    {item.completedQuantity} / {item.requestedQuantity}{' '}
                    {item.quantityUnit}
                  </td>
                  <td className="p-4">
                    {new Date(item.collectionWindowStartUtc).toLocaleString()}
                  </td>
                  <td className="p-4">{item.priority}</td>
                  <td className="p-4">
                    <Status value={item.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="mt-4 flex items-center justify-between">
        <Button
          disabled={page <= 1}
          onClick={() => {
            setPage((current) => current - 1)
          }}
          type="button"
          variant="outline"
        >
          Previous
        </Button>
        <span className="text-sm">
          Page {instructions.data?.page ?? page} of{' '}
          {instructions.data?.totalPages ?? 1}
        </span>
        <Button
          disabled={page >= (instructions.data?.totalPages ?? 1)}
          onClick={() => {
            setPage((current) => current + 1)
          }}
          type="button"
          variant="outline"
        >
          Next
        </Button>
      </div>
      {editor ? (
        <InstructionWizard
          existing={editor === 'new' ? undefined : editor}
          onClose={() => {
            setEditor(null)
          }}
          onSaved={(saved) => {
            setEditor(null)
            setSelectedId(saved.id)
          }}
        />
      ) : null}
    </section>
  )
}

function InstructionDetailView({
  instruction,
  loading,
  error,
  canManage,
  onBack,
  onEdit,
  children,
}: {
  instruction: InstructionDetail | undefined
  loading: boolean
  error: Error | null
  canManage: boolean
  onBack: () => void
  onEdit: (instruction: InstructionDetail) => void
  children: React.ReactNode
}) {
  const auth = useAuth()
  const client = useQueryClient()
  const [cancelReason, setCancelReason] = useState('')
  const [showCancel, setShowCancel] = useState(false)
  const [tripCount, setTripCount] = useState(1)
  const history = useQuery({
    queryKey: ['load-instruction-history', instruction?.id],
    queryFn: () =>
      instruction
        ? getInstructionHistory(auth.request, instruction.id)
        : Promise.reject(new Error('An instruction must be selected.')),
    enabled: instruction !== undefined,
  })
  const refresh = async (value: InstructionDetail) => {
    client.setQueryData(['load-instruction', value.id], value)
    await client.invalidateQueries({ queryKey: ['load-instructions'] })
    await client.invalidateQueries({
      queryKey: ['load-instruction-history', value.id],
    })
  }
  const plan = useMutation({
    mutationFn: () => {
      if (!instruction) throw new Error('An instruction must be selected.')
      return planInstruction(auth.request, instruction)
    },
    onSuccess: refresh,
  })
  const cancel = useMutation({
    mutationFn: () => {
      if (!instruction) throw new Error('An instruction must be selected.')
      return cancelInstruction(auth.request, instruction, cancelReason)
    },
    onSuccess: async (value) => {
      setShowCancel(false)
      await refresh(value)
    },
  })
  const trips = useMutation({
    mutationFn: () => {
      if (!instruction) throw new Error('An instruction must be selected.')
      return generateTrips(
        auth.request,
        instruction,
        tripCount,
        crypto.randomUUID(),
      )
    },
    onSuccess: refresh,
  })
  if (loading)
    return <div className="h-72 animate-pulse rounded-xl bg-slate-200" />
  if (error || !instruction) {
    return (
      <Message title="Instruction could not be loaded">
        {error?.message}
      </Message>
    )
  }
  const progress = Math.min(
    100,
    (instruction.completedQuantity / instruction.requestedQuantity) * 100,
  )
  const mutationError = plan.error ?? cancel.error ?? trips.error

  return (
    <section>
      <Button onClick={onBack} type="button" variant="ghost">
        <ChevronLeft aria-hidden="true" /> Back to instructions
      </Button>
      <div className="mt-4 flex flex-wrap items-start justify-between gap-4 rounded-xl border bg-white p-6">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-black">
              {instruction.instructionNumber}
            </h1>
            <Status value={instruction.status} />
          </div>
          <p>
            {instruction.customerName} · {instruction.contractNumber}
          </p>
          <p className="text-sm text-slate-500">
            {instruction.routeReference}: {instruction.sourceLocationName} →{' '}
            {instruction.destinationLocationName}
          </p>
        </div>
        {canManage ? (
          <div className="flex flex-wrap gap-2">
            {instruction.status !== 'Completed' &&
            instruction.status !== 'Cancelled' ? (
              <Button
                onClick={() => {
                  onEdit(instruction)
                }}
                type="button"
                variant="outline"
              >
                <Pencil aria-hidden="true" size={17} /> Edit
              </Button>
            ) : null}
            {instruction.status === 'Draft' ? (
              <Button
                onClick={() => {
                  plan.mutate()
                }}
                type="button"
              >
                Plan instruction
              </Button>
            ) : null}
            {instruction.status === 'Planned' ||
            instruction.status === 'InProgress' ? (
              <Button
                onClick={() => {
                  setShowCancel(true)
                }}
                type="button"
                variant="outline"
              >
                Cancel instruction
              </Button>
            ) : null}
          </div>
        ) : null}
      </div>
      {mutationError ? (
        <p
          className="mt-4 rounded-lg bg-red-50 p-3 font-bold text-red-800"
          role="alert"
        >
          {mutationError.message}
        </p>
      ) : null}
      <div className="mt-4 grid gap-4 md:grid-cols-4">
        <Metric
          label="Requested"
          value={`${String(instruction.requestedQuantity)} ${instruction.quantityUnit}`}
        />
        <Metric
          label="Completed"
          value={`${String(instruction.completedQuantity)} ${instruction.quantityUnit}`}
        />
        <Metric
          label="Remaining"
          value={`${String(instruction.remainingQuantity)} ${instruction.quantityUnit}`}
        />
        <Metric
          label={
            instruction.overDeliveredQuantity > 0
              ? 'Over-delivery'
              : 'Under-delivery'
          }
          tone={instruction.overDeliveredQuantity > 0 ? 'warning' : 'normal'}
          value={`${String(instruction.overDeliveredQuantity || instruction.underDeliveredQuantity)} ${instruction.quantityUnit}`}
        />
      </div>
      <div className="mt-4 rounded-xl border bg-white p-5">
        <div className="mb-2 flex justify-between text-sm font-bold">
          <span>Fulfilment progress</span>
          <span>{progress.toFixed(1)}%</span>
        </div>
        <div
          aria-label={`${progress.toFixed(1)}% complete`}
          className="h-4 overflow-hidden rounded-full bg-slate-200"
          role="progressbar"
        >
          <div
            className="h-full bg-emerald-600"
            style={{ width: `${String(progress)}%` }}
          />
        </div>
      </div>
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <div className="rounded-xl border bg-white p-5">
          <h2 className="text-lg font-black">Instruction details</h2>
          <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
            <Fact
              label="Commodity"
              value={`${instruction.commodityCode} · ${instruction.commodityName}`}
            />
            <Fact label="Priority" value={instruction.priority} />
            <Fact
              label="Collection"
              value={`${new Date(instruction.collectionWindowStartUtc).toLocaleString()} – ${new Date(instruction.collectionWindowEndUtc).toLocaleString()}`}
            />
            <Fact
              label="Delivery"
              value={`${new Date(instruction.deliveryWindowStartUtc).toLocaleString()} – ${new Date(instruction.deliveryWindowEndUtc).toLocaleString()}`}
            />
            <Fact
              label="Customer PO"
              value={instruction.customerPurchaseOrderReference || '—'}
            />
            <Fact
              label="Customer instruction"
              value={instruction.customerInstructionReference || '—'}
            />
          </dl>
          {instruction.specialInstructions ? (
            <p className="mt-4 rounded-lg bg-amber-50 p-3">
              {instruction.specialInstructions}
            </p>
          ) : null}
          {instruction.cancellationReason ? (
            <p className="mt-4 font-bold text-red-800">
              Cancelled: {instruction.cancellationReason}
            </p>
          ) : null}
        </div>
        <div className="rounded-xl border bg-white p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-lg font-black">Generated trips</h2>
            {canManage && instruction.status === 'Planned' ? (
              <div className="flex gap-2">
                <label>
                  <span className="sr-only">Trip count</span>
                  <input
                    aria-label="Trip count"
                    className="h-10 w-20 rounded-md border px-2"
                    min="1"
                    onChange={(event) => {
                      setTripCount(Number(event.target.value))
                    }}
                    type="number"
                    value={tripCount}
                  />
                </label>
                <Button
                  onClick={() => {
                    trips.mutate()
                  }}
                  type="button"
                >
                  Generate trips
                </Button>
              </div>
            ) : null}
          </div>
          {instruction.trips.length === 0 ? (
            <p className="mt-3 text-slate-500">No trips generated yet.</p>
          ) : (
            <ul className="mt-3 divide-y">
              {instruction.trips.map((trip) => (
                <li className="flex justify-between py-3" key={trip.id}>
                  <span>
                    <strong>{trip.tripNumber}</strong>
                    <span className="block text-xs text-slate-500">
                      {trip.status}
                    </span>
                  </span>
                  <span>
                    {trip.completedQuantity ?? trip.plannedQuantity}{' '}
                    {trip.quantityUnit}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
      {showCancel ? (
        <div className="mt-4 rounded-xl border border-red-200 bg-red-50 p-4">
          <label className="block font-bold">
            Cancellation reason
            <textarea
              className="mt-1 min-h-20 w-full rounded-md border bg-white p-3"
              onChange={(event) => {
                setCancelReason(event.target.value)
              }}
              value={cancelReason}
            />
          </label>
          <div className="mt-3 flex gap-2">
            <Button
              disabled={!cancelReason.trim()}
              onClick={() => {
                cancel.mutate()
              }}
              type="button"
            >
              Confirm cancellation
            </Button>
            <Button
              onClick={() => {
                setShowCancel(false)
              }}
              type="button"
              variant="outline"
            >
              Keep instruction
            </Button>
          </div>
        </div>
      ) : null}
      <div className="mt-4 rounded-xl border bg-white p-5">
        <h2 className="text-lg font-black">History</h2>
        {history.isLoading ? (
          <p>Loading history…</p>
        ) : (
          <ul className="mt-2 divide-y">
            {history.data?.map((item) => (
              <li className="flex justify-between py-2 text-sm" key={item.id}>
                <span>{item.eventType.replace('LoadInstruction.', '')}</span>
                <time dateTime={item.occurredAtUtc}>
                  {new Date(item.occurredAtUtc).toLocaleString()}
                </time>
              </li>
            ))}
          </ul>
        )}
      </div>
      {children}
    </section>
  )
}

function InstructionWizard({
  existing,
  onClose,
  onSaved,
}: {
  existing: InstructionDetail | undefined
  onClose: () => void
  onSaved: (instruction: InstructionDetail) => void
}) {
  const auth = useAuth()
  const locked = Boolean(existing?.trips.length)
  const [step, setStep] = useState(locked ? 4 : 1)
  const [contractId, setContractId] = useState(
    existing?.transportContractId ?? '',
  )
  const contracts = useQuery({
    queryKey: ['instruction-contract-options'],
    queryFn: () =>
      listContracts(auth.request, {
        page: 1,
        search: '',
        status: 'Active',
        customerId: '',
        pageSize: 100,
      }),
  })
  const contract = useQuery({
    queryKey: ['instruction-contract-option', contractId],
    queryFn: () => getContract(auth.request, contractId),
    enabled: contractId !== '',
  })
  const form = useForm<FormValues>({
    defaultValues: existing
      ? valuesFrom(existing)
      : {
          instructionNumber: `LI-${crypto.randomUUID().slice(0, 8).toUpperCase()}`,
          transportContractId: '',
          contractRouteId: '',
          commodityId: '',
          requestedQuantity: '30',
          quantityUnit: 'Tonnes',
          requestedTripCount: '1',
          collectionWindowStart: dateTime(tomorrow),
          collectionWindowEnd: dateTime(
            new Date(tomorrow.getTime() + 3_600_000),
          ),
          deliveryWindowStart: dateTime(
            new Date(tomorrow.getTime() + 7_200_000),
          ),
          deliveryWindowEnd: dateTime(
            new Date(tomorrow.getTime() + 14_400_000),
          ),
          customerPurchaseOrderReference: '',
          customerInstructionReference: '',
          priority: 'Normal',
          specialInstructions: '',
        },
  })
  const save = useMutation({
    mutationFn: async ({
      values,
      plan,
    }: {
      values: FormValues
      plan: boolean
    }) => {
      const selectedContract = contract.data
      if (!selectedContract) throw new Error('Select an active contract.')
      const saved = await saveInstruction(
        auth.request,
        {
          ...values,
          customerId: selectedContract.customerId,
          requestedQuantity: Number(values.requestedQuantity),
          requestedTripCount: values.requestedTripCount
            ? Number(values.requestedTripCount)
            : null,
          collectionWindowStart: new Date(
            values.collectionWindowStart,
          ).toISOString(),
          collectionWindowEnd: new Date(
            values.collectionWindowEnd,
          ).toISOString(),
          deliveryWindowStart: new Date(
            values.deliveryWindowStart,
          ).toISOString(),
          deliveryWindowEnd: new Date(values.deliveryWindowEnd).toISOString(),
          version: existing?.version,
        },
        existing?.id,
      )
      return plan && saved.status === 'Draft'
        ? planInstruction(auth.request, saved)
        : saved
    },
    onSuccess: onSaved,
  })
  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => {
      if (form.formState.isDirty) event.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => {
      window.removeEventListener('beforeunload', warn)
    }
  }, [form.formState.isDirty])
  const close = () => {
    if (
      !form.formState.isDirty ||
      window.confirm('Discard unsaved instruction changes?')
    )
      onClose()
  }
  const field = (
    name: keyof FormValues,
    label: string,
    type = 'text',
    required = true,
  ) => (
    <div>
      <FieldLabel help={instructionFieldHelp[name]} label={label} />
      <input
        aria-label={label}
        className="h-11 w-full rounded-md border px-3"
        type={type}
        {...form.register(name, { required })}
      />
    </div>
  )
  const selectedRouteId = useWatch({
    control: form.control,
    name: 'contractRouteId',
  })
  const selectedRoute = contract.data?.routes.find(
    (route) => route.id === selectedRouteId,
  )

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/45">
      <div
        aria-labelledby="instruction-wizard-title"
        aria-modal="true"
        className="h-full w-full max-w-3xl overflow-y-auto bg-white p-6 shadow-2xl"
        role="dialog"
      >
        <div className="flex justify-between">
          <div>
            <p className="text-sm font-bold text-emerald-700">
              Step {step} of 4
            </p>
            <h2 className="text-2xl font-black" id="instruction-wizard-title">
              {existing ? 'Edit' : 'Create'} load instruction
            </h2>
          </div>
          <Button
            aria-label="Close instruction wizard"
            onClick={close}
            type="button"
            variant="ghost"
          >
            <X />
          </Button>
        </div>
        <form
          className="mt-6"
          onSubmit={(event) =>
            void form.handleSubmit((values) => {
              save.mutate({ values, plan: false })
            })(event)
          }
        >
          {step === 1 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              {field('instructionNumber', 'Instruction number')}
              <div>
                <FieldLabel
                  help={instructionFieldHelp.transportContractId}
                  label="Transport contract"
                />
                <select
                  aria-label="Transport contract"
                  className="h-11 w-full rounded-md border bg-white px-3"
                  disabled={Boolean(existing?.trips.length)}
                  {...form.register('transportContractId', { required: true })}
                  onChange={(event) => {
                    form.setValue('transportContractId', event.target.value, {
                      shouldDirty: true,
                    })
                    setContractId(event.target.value)
                  }}
                >
                  <option value="">Select active contract</option>
                  {contracts.data?.items.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.contractNumber} · {item.customerName}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <FieldLabel
                  help={instructionFieldHelp.contractRouteId}
                  label="Route"
                />
                <select
                  aria-label="Route"
                  className="h-11 w-full rounded-md border bg-white px-3"
                  {...form.register('contractRouteId', { required: true })}
                >
                  <option value="">Select route</option>
                  {contract.data?.routes.map((route) => (
                    <option key={route.id} value={route.id}>
                      {route.reference}: {route.sourceLocationName} →{' '}
                      {route.destinationLocationName}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <FieldLabel
                  help={instructionFieldHelp.commodityId}
                  label="Commodity"
                />
                <select
                  aria-label="Commodity"
                  className="h-11 w-full rounded-md border bg-white px-3"
                  {...form.register('commodityId', { required: true })}
                >
                  <option value="">Select commodity</option>
                  {contract.data?.commodities.map((item) => (
                    <option key={item.commodityId} value={item.commodityId}>
                      {item.commodityCode} · {item.commodityName}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          ) : null}
          {step === 2 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              {field('requestedQuantity', 'Requested quantity', 'number')}
              <div>
                <FieldLabel
                  help={instructionFieldHelp.quantityUnit}
                  label="Quantity unit"
                />
                <select
                  aria-label="Quantity unit"
                  className="h-11 w-full rounded-md border bg-white px-3"
                  {...form.register('quantityUnit')}
                >
                  <option>Tonnes</option>
                  <option>Kilograms</option>
                </select>
              </div>
              {field(
                'requestedTripCount',
                'Requested trip count',
                'number',
                false,
              )}
              {field(
                'collectionWindowStart',
                'Collection window start',
                'datetime-local',
              )}
              {field(
                'collectionWindowEnd',
                'Collection window end',
                'datetime-local',
              )}
              {field(
                'deliveryWindowStart',
                'Delivery window start',
                'datetime-local',
              )}
              {field(
                'deliveryWindowEnd',
                'Delivery window end',
                'datetime-local',
              )}
            </div>
          ) : null}
          {step === 3 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <FieldLabel
                  help={instructionFieldHelp.priority}
                  label="Priority"
                />
                <select
                  aria-label="Priority"
                  className="h-11 w-full rounded-md border bg-white px-3"
                  {...form.register('priority')}
                >
                  <option>Low</option>
                  <option>Normal</option>
                  <option>High</option>
                  <option>Urgent</option>
                </select>
              </div>
              {field(
                'customerPurchaseOrderReference',
                'Customer purchase-order reference',
                'text',
                false,
              )}
              {field(
                'customerInstructionReference',
                'Customer instruction reference',
                'text',
                false,
              )}
              <div className="sm:col-span-2">
                <FieldLabel
                  help={instructionFieldHelp.specialInstructions}
                  label="Special instructions"
                />
                <textarea
                  aria-label="Special instructions"
                  className="min-h-32 w-full rounded-md border p-3"
                  {...form.register('specialInstructions')}
                />
              </div>
            </div>
          ) : null}
          {step === 4 ? (
            <div className="space-y-4" aria-label="Load instruction review">
              <div className="rounded-xl border bg-slate-50 p-5">
                <h3 className="text-lg font-black">Review and create</h3>
                {/* eslint-disable react-hooks/incompatible-library -- React Hook Form watch drives the review summary. */}
                <dl className="mt-4 grid gap-3 sm:grid-cols-2">
                  <Summary
                    label="Customer"
                    value={contract.data?.customerName ?? 'Select a contract'}
                  />
                  <Summary
                    label="Contract"
                    value={contract.data?.contractNumber ?? 'Not selected'}
                  />
                  <Summary
                    label="Planned quantity"
                    value={`${form.watch('requestedQuantity')} ${form.watch('quantityUnit')}`}
                  />
                  <Summary
                    label="Expected trips"
                    value={form.watch('requestedTripCount') || 'Not specified'}
                  />
                  <Summary
                    label="Collection weighing"
                    value={
                      selectedRoute?.sourceWeightCaptureRequired
                        ? 'Required'
                        : 'Not required'
                    }
                  />
                  <Summary
                    label="Delivery weighing"
                    value={
                      selectedRoute?.destinationWeightCaptureRequired
                        ? 'Required'
                        : 'Not required'
                    }
                  />
                  <Summary
                    label="Collection window"
                    value={`${form.watch('collectionWindowStart')} â€“ ${form.watch('collectionWindowEnd')}`}
                  />
                  <Summary
                    label="Delivery window"
                    value={`${form.watch('deliveryWindowStart')} â€“ ${form.watch('deliveryWindowEnd')}`}
                  />
                </dl>
                {/* eslint-enable react-hooks/incompatible-library */}
              </div>
              <p className="text-sm text-slate-600">
                Save as draft keeps this instruction editable. Plan load
                confirms it for trip generation.
              </p>
            </div>
          ) : null}
          {save.isError ? (
            <p className="mt-4 font-bold text-red-700" role="alert">
              {save.error.message}
            </p>
          ) : null}
          <div className="mt-8 flex justify-between">
            <Button
              disabled={step === 1 || locked}
              onClick={() => {
                setStep((value) => value - 1)
              }}
              type="button"
              variant="outline"
            >
              Back
            </Button>
            {step < 4 ? (
              <Button
                onClick={(event) => {
                  event.preventDefault()
                  setStep((value) => value + 1)
                }}
                type="button"
              >
                Continue
              </Button>
            ) : (
              <div className="flex flex-wrap gap-2">
                <Button
                  disabled={save.isPending || !contract.data}
                  type="submit"
                  variant="outline"
                >
                  {save.isPending
                    ? 'Saving…'
                    : existing
                      ? 'Save changes'
                      : 'Create draft instruction'}
                </Button>
                {!existing || existing.status === 'Draft' ? (
                  <Button
                    disabled={save.isPending || !contract.data}
                    onClick={() => {
                      void form.handleSubmit((values) => {
                        save.mutate({ values, plan: true })
                      })()
                    }}
                    type="button"
                  >
                    Plan load
                  </Button>
                ) : null}
              </div>
            )}
          </div>
        </form>
      </div>
    </div>
  )
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-bold tracking-wide text-slate-500 uppercase">
        {label}
      </dt>
      <dd className="mt-1 font-semibold text-slate-900">{value}</dd>
    </div>
  )
}

function valuesFrom(item: InstructionDetail): FormValues {
  const local = (value: string) => {
    const date = new Date(value)
    return new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
      .toISOString()
      .slice(0, 16)
  }
  return {
    instructionNumber: item.instructionNumber,
    transportContractId: item.transportContractId,
    contractRouteId: item.contractRouteId,
    commodityId: item.commodityId,
    requestedQuantity: String(item.requestedQuantity),
    quantityUnit: item.quantityUnit,
    requestedTripCount: item.requestedTripCount
      ? String(item.requestedTripCount)
      : '',
    collectionWindowStart: local(item.collectionWindowStartUtc),
    collectionWindowEnd: local(item.collectionWindowEndUtc),
    deliveryWindowStart: local(item.deliveryWindowStartUtc),
    deliveryWindowEnd: local(item.deliveryWindowEndUtc),
    customerPurchaseOrderReference: item.customerPurchaseOrderReference,
    customerInstructionReference: item.customerInstructionReference,
    priority: item.priority,
    specialInstructions: item.specialInstructions,
  }
}

function Filter({
  label,
  value,
  options,
  onChange,
}: {
  label: string
  value: string
  options: string[]
  onChange: (value: string) => void
}) {
  return (
    <label>
      <span className="sr-only">{label}</span>
      <select
        aria-label={label}
        className="h-11 rounded-md border bg-white px-3"
        onChange={(event) => {
          onChange(event.target.value)
        }}
        value={value}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option === 'all'
              ? label === 'Priority filter'
                ? 'All priorities'
                : 'All instruction statuses'
              : option}
          </option>
        ))}
      </select>
    </label>
  )
}
function Status({ value }: { value: string }) {
  return (
    <span className="inline-flex rounded-full bg-slate-100 px-2.5 py-1 text-xs font-black text-slate-700">
      {value}
    </span>
  )
}
function Metric({
  label,
  value,
  tone = 'normal',
}: {
  label: string
  value: string
  tone?: 'normal' | 'warning'
}) {
  return (
    <div
      className={`rounded-xl border bg-white p-4 ${tone === 'warning' ? 'border-amber-400' : ''}`}
    >
      <p className="text-xs font-bold text-slate-500 uppercase">{label}</p>
      <p className="mt-1 text-xl font-black">{value}</p>
    </div>
  )
}
function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="font-bold text-slate-500">{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}
function Message({
  title,
  children,
}: {
  title: string
  children?: React.ReactNode
}) {
  return (
    <div className="rounded-xl border bg-white p-8 text-center">
      <h2 className="text-xl font-black">{title}</h2>
      {children ? <p className="mt-2 text-slate-600">{children}</p> : null}
    </div>
  )
}
