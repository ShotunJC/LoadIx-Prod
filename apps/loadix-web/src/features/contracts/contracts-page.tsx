import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm, type UseFormRegisterReturn } from 'react-hook-form'
import { Calculator, ChevronLeft, Pencil, Plus, X } from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { FieldHelp, FieldLabel } from '@/components/ui/field-help'
import {
  createContract,
  getContract,
  getContractHistory,
  listContracts,
  previewRate,
  transitionContract,
  updateContract,
  type ContractDetail,
  type RateCalculation,
} from './contract-api'
import {
  listMasterData,
  saveMasterData,
  type MasterDataItem,
} from '../master-data/master-data-api'

interface WizardValues {
  customerId: string
  contractNumber: string
  name: string
  startDate: string
  endDate: string
  defaultCurrency: string
  paymentTermsDays: string
  weightTolerancePercent: string
  notes: string
  routeReference: string
  sourceLocationId: string
  destinationLocationId: string
  sourceWeightCaptureRequired: boolean
  destinationWeightCaptureRequired: boolean
  collectionInstructions: string
  deliveryInstructions: string
  commodityId: string
  manualQuantityPermitted: boolean
  rateType: string
  fixedAmount: string
  baseFee: string
  perTonneAmount: string
  minimumCharge: string
  fuelSurchargePercent: string
}

const contractFieldHelp: Record<keyof WizardValues, string> = {
  customerId:
    'The customer whose work, rates and billing terms this contract governs.',
  contractNumber: 'Your unique customer-facing reference for this agreement.',
  name: 'A short description that helps users recognise this contract.',
  startDate:
    'The first day on which loads may be planned against this contract.',
  endDate:
    'The final contract day; leave blank only for an open-ended agreement.',
  defaultCurrency:
    'The three-letter currency used for every rate on this contract, such as ZAR.',
  paymentTermsDays:
    'The number of days allowed for payment after an invoice is issued.',
  weightTolerancePercent:
    'The permitted difference between collection and delivery net weight before review is required.',
  notes: 'Internal commercial context visible to authorised LoadIx users.',
  routeReference: 'A short unique name for this collection-to-delivery route.',
  sourceLocationId: 'The site where the load is collected.',
  destinationLocationId: 'The site where the load is delivered.',
  sourceWeightCaptureRequired:
    'Select this when collection cannot be completed without a valid source weight.',
  destinationWeightCaptureRequired:
    'Select this when delivery cannot be completed without a valid destination weight.',
  collectionInstructions:
    'Standing access, safety or loading directions for the collection site.',
  deliveryInstructions:
    'Standing access, safety or offloading directions for the delivery site.',
  commodityId: 'The material or goods permitted on this contract route.',
  manualQuantityPermitted:
    'Allows an authorised supervisor to record an audited quantity when no measured quantity is available.',
  rateType:
    'Determines which trip quantity and fee structure are used to calculate the charge.',
  fixedAmount: 'The charge for each trip regardless of measured quantity.',
  baseFee: 'The fixed part of a charge before any per-tonne amount is added.',
  perTonneAmount: 'The amount charged for each billable tonne.',
  minimumCharge:
    'The lowest charge permitted after the rate and fuel surcharge are calculated.',
  fuelSurchargePercent:
    'The percentage added to the calculated transport charge for fuel recovery.',
}

const today = new Date().toISOString().slice(0, 10)
const nextYear = new Date(Date.now() + 365 * 86_400_000)
  .toISOString()
  .slice(0, 10)

export function ContractsPage() {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [customerId, setCustomerId] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [wizardOpen, setWizardOpen] = useState(false)
  const [editingContract, setEditingContract] = useState<ContractDetail | null>(
    null,
  )

  const customers = useLookup(auth.request, 'customers', 'tradingName')
  const contracts = useQuery({
    queryKey: ['contracts', page, search, status, customerId],
    queryFn: () =>
      listContracts(auth.request, { page, search, status, customerId }),
  })
  const detail = useQuery({
    queryKey: ['contract', selectedId],
    queryFn: () =>
      selectedId
        ? getContract(auth.request, selectedId)
        : Promise.reject(new Error('A contract must be selected.')),
    enabled: selectedId !== null,
  })
  const canManage = auth.user?.permissions.includes('Contract.Manage') === true

  if (!auth.user?.permissions.includes('Contract.View')) {
    return (
      <Message title="Access denied">
        You cannot view transport contracts.
      </Message>
    )
  }

  if (selectedId) {
    return (
      <>
        <ContractDetailView
          canManage={canManage}
          contract={detail.data}
          error={detail.error}
          loading={detail.isLoading}
          onBack={() => {
            setSelectedId(null)
          }}
          onEdit={setEditingContract}
        />
        {editingContract ? (
          <ContractWizard
            contract={editingContract}
            onClose={() => {
              setEditingContract(null)
            }}
            onCreated={(contract) => {
              setEditingContract(null)
              queryClient.setQueryData(['contract', contract.id], contract)
              void queryClient.invalidateQueries({ queryKey: ['contracts'] })
            }}
          />
        ) : null}
      </>
    )
  }

  return (
    <section>
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-bold tracking-[0.16em] text-emerald-700 uppercase">
            Commercial
          </p>
          <h1 className="mt-1 text-3xl font-black text-slate-950">
            Transport contracts
          </h1>
          <p className="mt-1 text-slate-600">
            Effective routes, commodities, tolerances and auditable pricing.
          </p>
        </div>
        {canManage ? (
          <Button
            onClick={() => {
              setWizardOpen(true)
            }}
            type="button"
          >
            <Plus aria-hidden="true" size={18} /> New contract
          </Button>
        ) : null}
      </div>

      <div className="mb-4 flex flex-wrap gap-3 rounded-xl border bg-white p-4">
        <label>
          <span className="sr-only">Search contracts</span>
          <input
            className="h-11 min-w-64 rounded-md border px-3"
            onChange={(event) => {
              setPage(1)
              setSearch(event.target.value)
            }}
            placeholder="Search number or name"
            type="search"
            value={search}
          />
        </label>
        <label>
          <span className="sr-only">Contract status</span>
          <select
            aria-label="Contract status"
            className="h-11 rounded-md border bg-white px-3"
            onChange={(event) => {
              setPage(1)
              setStatus(event.target.value)
            }}
            value={status}
          >
            {['all', 'Draft', 'Active', 'Suspended', 'Expired', 'Archived'].map(
              (option) => (
                <option key={option} value={option}>
                  {option === 'all' ? 'All statuses' : option}
                </option>
              ),
            )}
          </select>
        </label>
        <label>
          <span className="sr-only">Customer filter</span>
          <select
            aria-label="Customer filter"
            className="h-11 rounded-md border bg-white px-3"
            onChange={(event) => {
              setPage(1)
              setCustomerId(event.target.value)
            }}
            value={customerId}
          >
            <option value="">All customers</option>
            {customers.data?.items.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {itemText(customer, 'tradingName')}
              </option>
            ))}
          </select>
        </label>
      </div>

      {contracts.isError ? (
        <Message title="Contracts could not be loaded">
          {contracts.error.message}
        </Message>
      ) : (
        <ContractTable
          data={contracts.data}
          loading={contracts.isLoading}
          onOpen={setSelectedId}
          onPage={setPage}
        />
      )}

      {wizardOpen ? (
        <ContractWizard
          onClose={() => {
            setWizardOpen(false)
          }}
          onCreated={(contract) => {
            setWizardOpen(false)
            setSelectedId(contract.id)
            void queryClient.invalidateQueries({ queryKey: ['contracts'] })
          }}
        />
      ) : null}
    </section>
  )
}

function ContractTable({
  data,
  loading,
  onOpen,
  onPage,
}: {
  data: Awaited<ReturnType<typeof listContracts>> | undefined
  loading: boolean
  onOpen: (id: string) => void
  onPage: (page: number) => void
}) {
  if (loading) {
    return (
      <div aria-label="Loading contracts" className="space-y-3">
        {Array.from({ length: 5 }, (_, index) => (
          <div
            className="h-16 animate-pulse rounded-xl bg-slate-200"
            key={index}
          />
        ))}
      </div>
    )
  }
  if (!data?.items.length) {
    return (
      <Message title="No matching contracts">
        Adjust the filters or create the first contract.
      </Message>
    )
  }
  return (
    <>
      <div className="overflow-x-auto rounded-xl border bg-white">
        <table className="w-full min-w-190 text-left text-sm">
          <thead className="bg-slate-50 text-xs tracking-wide text-slate-600 uppercase">
            <tr>
              {['Contract', 'Customer', 'Period', 'Coverage', 'Status', ''].map(
                (heading) => (
                  <th className="px-4 py-3" key={heading} scope="col">
                    {heading}
                  </th>
                ),
              )}
            </tr>
          </thead>
          <tbody className="divide-y">
            {data.items.map((contract) => (
              <tr key={contract.id}>
                <td className="px-4 py-3">
                  <strong className="block">{contract.contractNumber}</strong>
                  <span className="text-slate-500">{contract.name}</span>
                </td>
                <td className="px-4 py-3">{contract.customerName}</td>
                <td className="px-4 py-3">
                  {contract.startDate} – {contract.endDate ?? 'Open-ended'}
                </td>
                <td className="px-4 py-3">
                  {contract.routeCount} routes · {contract.commodityCount}{' '}
                  commodities
                </td>
                <td className="px-4 py-3">
                  <ContractStatus status={contract.status} />
                </td>
                <td className="px-4 py-3 text-right">
                  <Button
                    onClick={() => {
                      onOpen(contract.id)
                    }}
                    type="button"
                    variant="outline"
                  >
                    View contract
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <nav
        aria-label="Contract pagination"
        className="mt-4 flex items-center justify-between"
      >
        <p className="text-sm text-slate-600">
          Page {data.page} of {Math.max(data.totalPages, 1)} · {data.totalCount}{' '}
          contracts
        </p>
        <div className="flex gap-2">
          <Button
            disabled={data.page === 1}
            onClick={() => {
              onPage(data.page - 1)
            }}
            variant="outline"
          >
            Previous
          </Button>
          <Button
            disabled={data.page >= data.totalPages}
            onClick={() => {
              onPage(data.page + 1)
            }}
            variant="outline"
          >
            Next
          </Button>
        </div>
      </nav>
    </>
  )
}

function ContractWizard({
  contract,
  onClose,
  onCreated,
}: {
  contract?: ContractDetail
  onClose: () => void
  onCreated: (contract: ContractDetail) => void
}) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [step, setStep] = useState(1)
  const customers = useLookup(auth.request, 'customers', 'tradingName')
  const locations = useLookup(auth.request, 'locations', 'name')
  const commodities = useLookup(auth.request, 'commodities', 'name')
  const [inlineType, setInlineType] = useState<
    'customer' | 'source' | 'destination' | 'commodity' | null
  >(null)
  const existingRoute = contract?.routes[0]
  const existingRate = contract?.rates[0]
  const form = useForm<WizardValues>({
    defaultValues: {
      customerId: contract?.customerId ?? '',
      contractNumber: contract?.contractNumber ?? '',
      name: contract?.name ?? '',
      startDate: contract?.startDate ?? today,
      endDate: contract?.endDate ?? nextYear,
      defaultCurrency: contract?.defaultCurrency ?? 'ZAR',
      paymentTermsDays: String(contract?.paymentTermsDays ?? 30),
      weightTolerancePercent: String(contract?.weightTolerancePercent ?? 1.5),
      notes: contract?.notes ?? '',
      routeReference: existingRoute?.reference ?? '',
      sourceLocationId: existingRoute?.sourceLocationId ?? '',
      destinationLocationId: existingRoute?.destinationLocationId ?? '',
      sourceWeightCaptureRequired:
        existingRoute?.sourceWeightCaptureRequired ?? true,
      destinationWeightCaptureRequired:
        existingRoute?.destinationWeightCaptureRequired ?? true,
      collectionInstructions: existingRoute?.collectionInstructions ?? '',
      deliveryInstructions: existingRoute?.deliveryInstructions ?? '',
      commodityId: contract?.commodities[0]?.commodityId ?? '',
      manualQuantityPermitted: contract?.manualQuantityPermitted ?? false,
      rateType: existingRate?.rateType ?? 'BasePlusPerTonne',
      fixedAmount: String(existingRate?.fixedAmount ?? ''),
      baseFee: String(existingRate?.baseFee ?? '500'),
      perTonneAmount: String(existingRate?.perTonneAmount ?? '100'),
      minimumCharge: String(existingRate?.minimumCharge ?? ''),
      fuelSurchargePercent: String(existingRate?.fuelSurchargePercent ?? ''),
    },
  })
  const create = useMutation({
    mutationFn: async ({
      values,
      activate,
    }: {
      values: WizardValues
      activate: boolean
    }) => {
      const routeId = existingRoute?.id ?? crypto.randomUUID()
      const optionalNumber = (value: string) =>
        value.trim() ? Number(value) : null
      const payload = {
        customerId: values.customerId,
        contractNumber: values.contractNumber,
        name: values.name,
        startDate: values.startDate,
        endDate: values.endDate || null,
        defaultCurrency: values.defaultCurrency.toUpperCase(),
        paymentTermsDays: Number(values.paymentTermsDays),
        weightTolerancePercent: Number(values.weightTolerancePercent),
        manualQuantityPermitted: values.manualQuantityPermitted,
        notes: values.notes || null,
        routes: [
          {
            id: routeId,
            reference: values.routeReference,
            sourceLocationId: values.sourceLocationId,
            destinationLocationId: values.destinationLocationId,
            sourceWeightCaptureRequired: values.sourceWeightCaptureRequired,
            destinationWeightCaptureRequired:
              values.destinationWeightCaptureRequired,
            collectionInstructions: values.collectionInstructions || null,
            deliveryInstructions: values.deliveryInstructions || null,
          },
        ],
        commodityIds: [values.commodityId],
        rates: [
          {
            id: existingRate?.id ?? crypto.randomUUID(),
            contractRouteId: routeId,
            rateType: values.rateType,
            effectiveFrom: values.startDate,
            effectiveTo: values.endDate || null,
            fixedAmount: optionalNumber(values.fixedAmount),
            baseFee: optionalNumber(values.baseFee),
            perTonneAmount: optionalNumber(values.perTonneAmount),
            minimumCharge: optionalNumber(values.minimumCharge),
            fuelSurchargePercent: optionalNumber(values.fuelSurchargePercent),
            roundingDecimalPlaces: 2,
            roundingMode: 'AwayFromZero',
          },
        ],
        version: contract?.version ?? null,
      }
      const saved = contract
        ? await updateContract(auth.request, contract.id, payload)
        : await createContract(auth.request, payload)
      return activate && saved.status !== 'Active'
        ? transitionContract(auth.request, saved, 'activate')
        : saved
    },
    onSuccess: onCreated,
  })
  const close = () => {
    if (
      !form.formState.isDirty ||
      window.confirm('Discard unsaved contract changes?')
    ) {
      onClose()
    }
  }
  useEffect(() => {
    const warning = (event: BeforeUnloadEvent) => {
      if (form.formState.isDirty) event.preventDefault()
    }
    window.addEventListener('beforeunload', warning)
    return () => {
      window.removeEventListener('beforeunload', warning)
    }
  }, [form.formState.isDirty])

  const field = (
    name: keyof WizardValues,
    label: string,
    type = 'text',
    required = true,
  ) => (
    <div className="block">
      <FieldLabel help={contractFieldHelp[name]} label={label} />
      <input
        aria-label={label}
        className="h-11 w-full rounded-md border px-3"
        type={type}
        {...form.register(name, {
          required: required ? `${label} is required.` : false,
        })}
      />
    </div>
  )

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/45">
      <div
        aria-labelledby="contract-wizard-title"
        aria-modal="true"
        className="h-full w-full max-w-3xl overflow-y-auto bg-white p-6 shadow-2xl"
        role="dialog"
      >
        <div className="mb-6 flex items-start justify-between">
          <div>
            <p className="text-sm font-bold text-emerald-700">
              Step {step} of 5
            </p>
            <h2 className="text-2xl font-black" id="contract-wizard-title">
              {contract
                ? 'Edit transport contract'
                : 'Create transport contract'}
            </h2>
          </div>
          <Button
            aria-label="Close contract wizard"
            onClick={close}
            variant="ghost"
          >
            <X />
          </Button>
        </div>
        <form
          onSubmit={(event) => {
            void form.handleSubmit((values) => {
              create.mutate({ values, activate: false })
            })(event)
          }}
        >
          {step === 1 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <LookupSelect
                help={contractFieldHelp.customerId}
                items={customers.data?.items ?? []}
                label="Customer"
                name="customerId"
                register={form.register}
                textKey="tradingName"
              />
              <InlineCreateAction
                label="Create new customer"
                onClick={() => {
                  setInlineType('customer')
                }}
              />
              {field('contractNumber', 'Contract number')}
              {field('name', 'Contract name')}
              {field('startDate', 'Start date', 'date')}
              {field('endDate', 'End date', 'date', false)}
              {field('defaultCurrency', 'Currency')}
              {field('paymentTermsDays', 'Payment terms (days)', 'number')}
              <div className="sm:col-span-2">
                <FieldLabel help={contractFieldHelp.notes} label="Notes" />
                <textarea
                  aria-label="Notes"
                  className="min-h-24 w-full rounded-md border p-3"
                  {...form.register('notes')}
                />
              </div>
            </div>
          ) : null}
          {step === 2 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              {field('routeReference', 'Route reference')}
              <LookupSelect
                help={contractFieldHelp.sourceLocationId}
                items={locations.data?.items ?? []}
                label="Source location"
                name="sourceLocationId"
                register={form.register}
                textKey="name"
              />
              <InlineCreateAction
                label="Create collection location"
                onClick={() => {
                  setInlineType('source')
                }}
              />
              <LookupSelect
                help={contractFieldHelp.destinationLocationId}
                items={locations.data?.items ?? []}
                label="Destination location"
                name="destinationLocationId"
                register={form.register}
                textKey="name"
              />
              <InlineCreateAction
                label="Create delivery location"
                onClick={() => {
                  setInlineType('destination')
                }}
              />
              <CheckField
                help={contractFieldHelp.sourceWeightCaptureRequired}
                label="Collection weight capture required"
                registration={form.register('sourceWeightCaptureRequired')}
              />
              <CheckField
                help={contractFieldHelp.destinationWeightCaptureRequired}
                label="Delivery weight capture required"
                registration={form.register('destinationWeightCaptureRequired')}
              />
              <div>
                <FieldLabel
                  help={contractFieldHelp.collectionInstructions}
                  label="Collection instructions"
                />
                <textarea
                  aria-label="Collection instructions"
                  className="min-h-20 w-full rounded-md border p-3"
                  {...form.register('collectionInstructions')}
                />
              </div>
              <div>
                <FieldLabel
                  help={contractFieldHelp.deliveryInstructions}
                  label="Delivery instructions"
                />
                <textarea
                  aria-label="Delivery instructions"
                  className="min-h-20 w-full rounded-md border p-3"
                  {...form.register('deliveryInstructions')}
                />
              </div>
            </div>
          ) : null}
          {step === 3 ? (
            <div className="space-y-4">
              <LookupSelect
                help={contractFieldHelp.commodityId}
                items={commodities.data?.items ?? []}
                label="Permitted commodity"
                name="commodityId"
                register={form.register}
                textKey="name"
              />
              <InlineCreateAction
                label="Create new commodity"
                onClick={() => {
                  setInlineType('commodity')
                }}
              />
              {field(
                'weightTolerancePercent',
                'Weight tolerance (%)',
                'number',
              )}
              <CheckField
                help={contractFieldHelp.manualQuantityPermitted}
                label="Allow authorised manual quantity capture"
                registration={form.register('manualQuantityPermitted')}
              />
              <p className="text-sm text-slate-600">
                Manual quantities are audited and require the Trip Override
                permission.
              </p>
            </div>
          ) : null}
          {step === 4 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <FieldLabel
                  help={contractFieldHelp.rateType}
                  label="Rate type"
                />
                <select
                  aria-label="Rate type"
                  className="h-11 w-full rounded-md border bg-white px-3"
                  {...form.register('rateType', { required: true })}
                >
                  <option value="FixedPerTrip">Fixed amount per trip</option>
                  <option value="SourceNetTonne">Source net tonne</option>
                  <option value="DestinationNetTonne">
                    Destination net tonne
                  </option>
                  <option value="ReconciledTonne">Reconciled tonne</option>
                  <option value="BasePlusPerTonne">Base plus per tonne</option>
                  <option value="ManualQuantityPerTonne">
                    Authorised manual quantity per tonne
                  </option>
                </select>
              </div>
              {field('fixedAmount', 'Fixed amount', 'number', false)}
              {field('baseFee', 'Base fee', 'number', false)}
              {field('perTonneAmount', 'Amount per tonne', 'number', false)}
              {field('minimumCharge', 'Minimum charge', 'number', false)}
              {field(
                'fuelSurchargePercent',
                'Fuel surcharge (%)',
                'number',
                false,
              )}
              <p className="rounded-lg bg-slate-100 p-4 text-sm sm:col-span-2">
                Money rounds to two decimals, midpoint away from zero. Fuel is
                applied before the minimum-charge floor.
              </p>
            </div>
          ) : null}
          {step === 5 ? (
            <div className="space-y-4" aria-label="Contract review">
              {contract?.status === 'Active' ? (
                <p className="rounded-lg border border-amber-300 bg-amber-50 p-4 text-sm font-bold text-amber-900">
                  Changes apply to future trips only. Existing trips retain
                  their saved rate and route snapshot.
                </p>
              ) : null}
              <Panel title="Review and save">
                <dl className="grid gap-3 sm:grid-cols-2">
                  {/* eslint-disable react-hooks/incompatible-library -- React Hook Form watch drives the review summary. */}
                  <Term
                    label="Contract"
                    value={`${form.watch('contractNumber')} Â· ${form.watch('name')}`}
                  />
                  <Term
                    label="Period"
                    value={`${form.watch('startDate')} â€“ ${form.watch('endDate') || 'Open'}`}
                  />
                  <Term label="Route" value={form.watch('routeReference')} />
                  <Term
                    label="Rate"
                    value={rateLabel(form.watch('rateType'))}
                  />
                  <Term
                    label="Collection weighing"
                    value={
                      form.watch('sourceWeightCaptureRequired')
                        ? 'Required'
                        : 'Not required'
                    }
                  />
                  <Term
                    label="Delivery weighing"
                    value={
                      form.watch('destinationWeightCaptureRequired')
                        ? 'Required'
                        : 'Not required'
                    }
                  />
                  {/* eslint-enable react-hooks/incompatible-library */}
                </dl>
              </Panel>
            </div>
          ) : null}
          {form.formState.errors.root ? (
            <p role="alert">{form.formState.errors.root.message}</p>
          ) : null}
          {create.isError ? (
            <p className="mt-4 font-bold text-red-700" role="alert">
              {create.error.message}
            </p>
          ) : null}
          <div className="mt-8 flex justify-between">
            <Button
              disabled={step === 1}
              onClick={() => {
                setStep((current) => current - 1)
              }}
              type="button"
              variant="outline"
            >
              Back
            </Button>
            {step < 5 ? (
              <Button
                onClick={(event) => {
                  event.preventDefault()
                  setStep((current) => current + 1)
                }}
                type="button"
              >
                Continue
              </Button>
            ) : (
              <div className="flex flex-wrap gap-2">
                <Button
                  disabled={create.isPending}
                  type="submit"
                  variant="outline"
                >
                  {create.isPending ? 'Creating…' : 'Create draft contract'}
                </Button>
                <Button
                  disabled={create.isPending}
                  onClick={() => {
                    void form.handleSubmit((values) => {
                      create.mutate({ values, activate: true })
                    })()
                  }}
                  type="button"
                >
                  Save and activate
                </Button>
              </div>
            )}
          </div>
        </form>
        {inlineType ? (
          <InlineMasterDialog
            customerId={form.getValues('customerId')}
            onClose={() => {
              setInlineType(null)
            }}
            onSaved={(item) => {
              const fieldName =
                inlineType === 'customer'
                  ? 'customerId'
                  : inlineType === 'commodity'
                    ? 'commodityId'
                    : inlineType === 'source'
                      ? 'sourceLocationId'
                      : 'destinationLocationId'
              form.setValue(fieldName, item.id, { shouldDirty: true })
              const resource =
                inlineType === 'customer'
                  ? 'customers'
                  : inlineType === 'commodity'
                    ? 'commodities'
                    : 'locations'
              void queryClient.invalidateQueries({
                queryKey: ['contract-lookup', resource],
              })
              setInlineType(null)
            }}
            type={inlineType}
          />
        ) : null}
      </div>
    </div>
  )
}

function ContractDetailView({
  contract,
  loading,
  error,
  canManage,
  onBack,
  onEdit,
}: {
  contract: ContractDetail | undefined
  loading: boolean
  error: Error | null
  canManage: boolean
  onBack: () => void
  onEdit: (contract: ContractDetail) => void
}) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [source, setSource] = useState('30')
  const [destination, setDestination] = useState('30')
  const [reconciled, setReconciled] = useState('30')
  const [calculation, setCalculation] = useState<RateCalculation | null>(null)
  const contractId = contract?.id
  const history = useQuery({
    queryKey: ['contract-history', contractId],
    queryFn: () =>
      contractId
        ? getContractHistory(auth.request, contractId)
        : Promise.reject(new Error('A contract must be selected.')),
    enabled: contractId !== undefined,
  })
  const transition = useMutation({
    mutationFn: (action: 'activate' | 'suspend') => {
      if (!contract) throw new Error('A contract must be selected.')
      return transitionContract(auth.request, contract, action)
    },
    onSuccess: (updated) => {
      queryClient.setQueryData(['contract', updated.id], updated)
      void queryClient.invalidateQueries({ queryKey: ['contracts'] })
      void queryClient.invalidateQueries({
        queryKey: ['contract-history', updated.id],
      })
    },
  })
  const preview = useMutation({
    mutationFn: () => {
      if (!contract || !contract.rates[0]) {
        throw new Error('The contract does not have a rate to preview.')
      }
      return previewRate(auth.request, contract.id, {
        rateId: contract.rates[0].id,
        sourceNetTonnes: Number(source),
        destinationNetTonnes: Number(destination),
        reconciledTonnes: Number(reconciled),
        calculationDate: today,
      })
    },
    onSuccess: setCalculation,
  })

  if (loading)
    return (
      <Message title="Loading contract">Fetching commercial terms.</Message>
    )
  if (error || !contract) {
    return (
      <Message title="Contract could not be loaded">{error?.message}</Message>
    )
  }
  return (
    <section>
      <Button onClick={onBack} variant="ghost">
        <ChevronLeft /> Back to contracts
      </Button>
      <div className="mt-4 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-black">{contract.contractNumber}</h1>
            <ContractStatus status={contract.status} />
          </div>
          <p className="mt-1 text-lg text-slate-600">{contract.name}</p>
          <p className="mt-1 text-sm text-slate-500">{contract.customerName}</p>
        </div>
        {canManage ? (
          <div className="flex gap-2">
            <Button
              onClick={() => {
                onEdit(contract)
              }}
              variant="outline"
            >
              <Pencil size={18} /> Edit contract
            </Button>
            {contract.status === 'Draft' || contract.status === 'Suspended' ? (
              <Button
                disabled={transition.isPending}
                onClick={() => {
                  transition.mutate('activate')
                }}
              >
                Activate contract
              </Button>
            ) : null}
            {contract.status === 'Active' ? (
              <Button
                disabled={transition.isPending}
                onClick={() => {
                  transition.mutate('suspend')
                }}
                variant="outline"
              >
                Suspend contract
              </Button>
            ) : null}
          </div>
        ) : null}
      </div>
      {transition.isError ? (
        <p className="mt-4 font-bold text-red-700" role="alert">
          {transition.error.message}
        </p>
      ) : null}

      <div className="mt-6 grid gap-4 md:grid-cols-4">
        <Metric
          label="Period"
          value={`${contract.startDate} – ${contract.endDate ?? 'Open'}`}
        />
        <Metric label="Currency" value={contract.defaultCurrency} />
        <Metric
          label="Payment terms"
          value={`${String(contract.paymentTermsDays)} days`}
        />
        <Metric
          label="Weight tolerance"
          value={`${String(contract.weightTolerancePercent)}%`}
        />
      </div>

      <div className="mt-6 grid gap-6 xl:grid-cols-2">
        <Panel title="Routes">
          {contract.routes.map((route) => (
            <div className="border-b py-3 last:border-0" key={route.id}>
              <strong>{route.reference}</strong>
              <p className="text-sm text-slate-600">
                {route.sourceLocationName} → {route.destinationLocationName}
              </p>
              <p className="mt-1 text-xs text-slate-500">
                Collection weighing:{' '}
                {route.sourceWeightCaptureRequired
                  ? 'required'
                  : 'not required'}{' '}
                · Delivery weighing:{' '}
                {route.destinationWeightCaptureRequired
                  ? 'required'
                  : 'not required'}
              </p>
            </div>
          ))}
        </Panel>
        <Panel title="Permitted commodities">
          <div className="flex flex-wrap gap-2">
            {contract.commodities.map((commodity) => (
              <span
                className="rounded-full bg-emerald-100 px-3 py-1 text-sm font-bold text-emerald-800"
                key={commodity.commodityId}
              >
                {commodity.commodityCode} · {commodity.commodityName}
              </span>
            ))}
          </div>
        </Panel>
        <Panel title="Rate configuration">
          {contract.rates.map((rate) => (
            <div key={rate.id}>
              <strong>{rateLabel(rate.rateType)}</strong>
              <p className="text-sm text-slate-600">
                {rate.effectiveFrom} – {rate.effectiveTo ?? 'Open'}
              </p>
              <dl className="mt-3 grid grid-cols-2 gap-2 text-sm">
                <Term label="Fixed" value={rate.fixedAmount} />
                <Term label="Base" value={rate.baseFee} />
                <Term label="Per tonne" value={rate.perTonneAmount} />
                <Term label="Minimum" value={rate.minimumCharge} />
                <Term
                  label="Fuel surcharge"
                  value={rate.fuelSurchargePercent}
                  suffix="%"
                />
                <Term
                  label="Rounding"
                  value={`${String(rate.roundingDecimalPlaces)} dp · ${rate.roundingMode}`}
                />
              </dl>
            </div>
          ))}
        </Panel>
        <Panel title="Rate preview calculator">
          <div className="grid grid-cols-3 gap-3">
            {[
              ['Source tonnes', source, setSource],
              ['Destination tonnes', destination, setDestination],
              ['Reconciled tonnes', reconciled, setReconciled],
            ].map(([label, value, setter]) => (
              <label key={label as string}>
                <span className="mb-1 block text-xs font-bold">
                  {label as string}
                </span>
                <input
                  className="h-10 w-full rounded-md border px-2"
                  onChange={(event) => {
                    ;(setter as (value: string) => void)(event.target.value)
                  }}
                  type="number"
                  value={value as string}
                />
              </label>
            ))}
          </div>
          <Button
            className="mt-4"
            disabled={!contract.rates.length || preview.isPending}
            onClick={() => {
              preview.mutate()
            }}
          >
            <Calculator size={18} /> Calculate preview
          </Button>
          {preview.isError ? (
            <p className="mt-3 text-sm font-bold text-red-700">
              {preview.error.message}
            </p>
          ) : null}
          {calculation ? (
            <div className="mt-4 rounded-lg bg-slate-950 p-4 text-white">
              <p className="text-sm text-slate-300">Calculated charge</p>
              <p className="text-2xl font-black">
                {calculation.currencyCode} {calculation.totalAmount.toFixed(2)}
              </p>
              <ul className="mt-3 space-y-1 text-xs text-slate-300">
                {calculation.breakdown.map((line) => (
                  <li className="flex justify-between" key={line.code}>
                    <span>{line.description}</span>
                    <span>{line.amount.toFixed(2)}</span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </Panel>
      </div>

      <Panel className="mt-6" title="Contract history">
        {history.isLoading ? <p>Loading history…</p> : null}
        {history.data?.length === 0 ? <p>No history recorded.</p> : null}
        <ol className="divide-y">
          {history.data?.map((entry) => (
            <li className="flex justify-between gap-4 py-3" key={entry.id}>
              <span className="font-bold">
                {entry.eventType.replace('Contract.', '')}
              </span>
              <time className="text-sm text-slate-500">
                {new Date(entry.occurredAtUtc).toLocaleString()}
              </time>
            </li>
          ))}
        </ol>
      </Panel>
    </section>
  )
}

function useLookup(
  request: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
  resource: string,
  sort: string,
) {
  return useQuery({
    queryKey: ['contract-lookup', resource],
    queryFn: () =>
      listMasterData(request, resource, {
        page: 1,
        pageSize: 100,
        search: '',
        status: 'active',
        sort,
        direction: 'asc',
      }),
  })
}

function LookupSelect({
  items,
  help,
  label,
  name,
  register,
  textKey,
}: {
  items: MasterDataItem[]
  help: string
  label: string
  name: keyof WizardValues
  register: ReturnType<typeof useForm<WizardValues>>['register']
  textKey: string
}) {
  const id = `contract-${name}`
  return (
    <div>
      <div>
        <FieldLabel help={help} label={label} />
      </div>
      <select
        aria-label={label}
        className="h-11 w-full rounded-md border bg-white px-3"
        id={id}
        {...register(name, { required: `${label} is required.` })}
      >
        <option value="">Select {label.toLowerCase()}</option>
        {items.map((item) => (
          <option key={item.id} value={item.id}>
            {itemText(item, textKey)}
          </option>
        ))}
      </select>
    </div>
  )
}

function InlineCreateAction({
  label,
  onClick,
}: {
  label: string
  onClick: () => void
}) {
  return (
    <Button onClick={onClick} type="button" variant="ghost">
      <Plus size={16} /> {label}
    </Button>
  )
}

function CheckField({
  label,
  help,
  registration,
}: {
  label: string
  help: string
  registration: UseFormRegisterReturn
}) {
  return (
    <div className="flex min-h-11 items-center gap-3 rounded-md border p-3 text-sm font-bold">
      <input
        aria-label={label}
        className="size-5"
        type="checkbox"
        {...registration}
      />
      {label}
      <FieldHelp label={label} text={help} />
    </div>
  )
}

function InlineMasterDialog({
  type,
  customerId,
  onClose,
  onSaved,
}: {
  type: 'customer' | 'source' | 'destination' | 'commodity'
  customerId: string
  onClose: () => void
  onSaved: (item: MasterDataItem) => void
}) {
  const auth = useAuth()
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [registration, setRegistration] = useState('')
  const [city, setCity] = useState('')
  const [addressLine, setAddressLine] = useState('')
  const resource =
    type === 'customer'
      ? 'customers'
      : type === 'commodity'
        ? 'commodities'
        : 'locations'
  const save = useMutation({
    mutationFn: () => {
      const address = {
        line1: addressLine,
        line2: null,
        city,
        provinceOrRegion: 'Gauteng',
        postalCode: '0001',
        countryCode: 'ZA',
      }
      const payload =
        type === 'customer'
          ? {
              legalName: name,
              tradingName: name,
              registrationNumber: registration,
              taxNumber: null,
              paymentTermsDays: 30,
              billingAddress: address,
              contacts: [],
              notes: null,
            }
          : type === 'commodity'
            ? { name, code, defaultWeightUnit: 'Tonnes' }
            : {
                customerId: customerId || null,
                name,
                code,
                type: type === 'source' ? 'CollectionSite' : 'DeliverySite',
                address,
                latitude: null,
                longitude: null,
                geofenceRadiusMetres: null,
                siteInstructions: null,
                contactName: null,
                contactPhoneNumber: null,
                operatingHours: null,
              }
      return saveMasterData(auth.request, resource, payload)
    },
    onSuccess: onSaved,
  })
  const needsAddress = type !== 'commodity'
  return (
    <div className="fixed inset-0 z-60 grid place-items-center bg-slate-950/55 p-4">
      <form
        aria-labelledby="inline-master-title"
        aria-modal="true"
        className="w-full max-w-lg space-y-4 rounded-xl bg-white p-6 shadow-2xl"
        onSubmit={(event) => {
          event.preventDefault()
          save.mutate()
        }}
        role="dialog"
      >
        <div className="flex items-start justify-between">
          <h3 className="text-xl font-black" id="inline-master-title">
            Create{' '}
            {type === 'customer'
              ? 'customer'
              : type === 'commodity'
                ? 'commodity'
                : 'location'}
          </h3>
          <Button
            aria-label="Close"
            onClick={onClose}
            type="button"
            variant="ghost"
          >
            <X />
          </Button>
        </div>
        <div className="block text-sm font-bold">
          <FieldLabel
            help={
              type === 'customer'
                ? 'The customer trading name shown throughout LoadIx.'
                : type === 'commodity'
                  ? 'The recognised name of the transported material or goods.'
                  : 'The operating name users will recognise for this site.'
            }
            label="Name"
          />
          <input
            aria-label="Name"
            className="mt-1 h-11 w-full rounded-md border px-3"
            onChange={(event) => {
              setName(event.target.value)
            }}
            required
            value={name}
          />
        </div>
        {type === 'customer' ? (
          <div className="block text-sm font-bold">
            <FieldLabel
              help="The customer company or business registration number."
              label="Registration number"
            />
            <input
              aria-label="Registration number"
              className="mt-1 h-11 w-full rounded-md border px-3"
              onChange={(event) => {
                setRegistration(event.target.value)
              }}
              required
              value={registration}
            />
          </div>
        ) : (
          <div className="block text-sm font-bold">
            <FieldLabel
              help="A short unique reference used in lists, documents and searches."
              label="Code"
            />
            <input
              aria-label="Code"
              className="mt-1 h-11 w-full rounded-md border px-3"
              onChange={(event) => {
                setCode(event.target.value)
              }}
              required
              value={code}
            />
          </div>
        )}
        {needsAddress ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="block text-sm font-bold">
              <FieldLabel
                help="The primary physical address used to identify and route to this site."
                label="Street address"
              />
              <input
                aria-label="Street address"
                className="mt-1 h-11 w-full rounded-md border px-3"
                onChange={(event) => {
                  setAddressLine(event.target.value)
                }}
                required
                value={addressLine}
              />
            </div>
            <div className="block text-sm font-bold">
              <FieldLabel
                help="The town or city used for routing and correspondence."
                label="City"
              />
              <input
                aria-label="City"
                className="mt-1 h-11 w-full rounded-md border px-3"
                onChange={(event) => {
                  setCity(event.target.value)
                }}
                required
                value={city}
              />
            </div>
          </div>
        ) : null}
        {save.isError ? (
          <p className="text-sm font-bold text-red-700" role="alert">
            {save.error.message}
          </p>
        ) : null}
        <div className="flex justify-end gap-2">
          <Button onClick={onClose} type="button" variant="outline">
            Cancel
          </Button>
          <Button disabled={save.isPending} type="submit">
            {save.isPending ? 'Savingâ€¦' : 'Create and select'}
          </Button>
        </div>
      </form>
    </div>
  )
}

function ContractStatus({ status }: { status: string }) {
  const style =
    status === 'Active'
      ? 'bg-emerald-100 text-emerald-800'
      : status === 'Draft'
        ? 'bg-blue-100 text-blue-800'
        : status === 'Suspended'
          ? 'bg-amber-100 text-amber-800'
          : 'bg-slate-200 text-slate-700'
  return (
    <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${style}`}>
      {status}
    </span>
  )
}

function Panel({
  title,
  children,
  className = '',
}: {
  title: string
  children: React.ReactNode
  className?: string
}) {
  return (
    <section className={`rounded-xl border bg-white p-5 ${className}`}>
      <h2 className="mb-4 text-lg font-black">{title}</h2>
      {children}
    </section>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border bg-white p-4">
      <p className="text-xs font-bold text-slate-500 uppercase">{label}</p>
      <p className="mt-1 font-black">{value}</p>
    </div>
  )
}

function Term({
  label,
  value,
  suffix = '',
}: {
  label: string
  value: number | string | null
  suffix?: string
}) {
  return (
    <div>
      <dt className="text-slate-500">{label}</dt>
      <dd className="font-bold">
        {value === null ? '—' : `${String(value)}${suffix}`}
      </dd>
    </div>
  )
}

function Message({
  title,
  children,
}: {
  title: string
  children: React.ReactNode
}) {
  return (
    <div className="rounded-xl border border-dashed bg-white px-6 py-12 text-center">
      <h2 className="text-lg font-black">{title}</h2>
      <div className="mt-1 text-sm text-slate-600">{children}</div>
    </div>
  )
}

function itemText(item: MasterDataItem, key: string) {
  const value = item[key]
  return typeof value === 'string' ? value : item.reference
}

function rateLabel(value: string) {
  const labels: Record<string, string> = {
    FixedPerTrip: 'Fixed amount per trip',
    SourceNetTonne: 'Amount per source net tonne',
    DestinationNetTonne: 'Amount per destination net tonne',
    ReconciledTonne: 'Amount per reconciled tonne',
    BasePlusPerTonne: 'Base fee plus per-tonne fee',
  }
  return labels[value] ?? value
}
