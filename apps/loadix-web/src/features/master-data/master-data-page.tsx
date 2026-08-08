import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Search, X } from 'lucide-react'
import { z } from 'zod'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import { FieldHelp, FieldLabel } from '@/components/ui/field-help'
import { DataTable, type DataColumn } from './data-table'
import {
  listMasterData,
  saveMasterData,
  setMasterDataStatus,
  type MasterDataItem,
} from './master-data-api'
import { StatusBadge } from './status-badge'

export type MasterDataModule =
  | 'branches'
  | 'customers'
  | 'locations'
  | 'commodities'
  | 'drivers'
  | 'vehicles'
  | 'trailers'

interface ModuleDefinition {
  title: string
  singular: string
  description: string
  viewPermission: string
  managePermission: string
  columns: DataColumn[]
  sortOptions: { value: string; label: string }[]
}

function text(item: MasterDataItem, key: string) {
  const value = item[key]
  return typeof value === 'string' || typeof value === 'number'
    ? String(value)
    : '—'
}

const statusColumn: DataColumn = {
  key: 'status',
  label: 'Status',
  render: (item) => <StatusBadge active={item.isActive} />,
}

const definitions: Record<MasterDataModule, ModuleDefinition> = {
  branches: {
    title: 'Branches',
    singular: 'branch',
    description: 'Operational branches, addresses and contact channels.',
    viewPermission: 'Tenant.View',
    managePermission: 'Tenant.Manage',
    sortOptions: [
      { value: 'name', label: 'Name' },
      { value: 'code', label: 'Code' },
    ],
    columns: [
      {
        key: 'reference',
        label: 'Branch code',
        render: (item) => (
          <span className="font-mono font-bold">{item.reference}</span>
        ),
      },
      { key: 'name', label: 'Name', render: (item) => text(item, 'name') },
      {
        key: 'contactEmail',
        label: 'Contact',
        render: (item) => text(item, 'contactEmail'),
      },
      statusColumn,
    ],
  },
  customers: {
    title: 'Customers',
    singular: 'customer',
    description: 'Legal, billing and operational customer information.',
    viewPermission: 'Customer.View',
    managePermission: 'Customer.Manage',
    sortOptions: [
      { value: 'legalName', label: 'Legal name' },
      { value: 'tradingName', label: 'Trading name' },
      { value: 'reference', label: 'Registration' },
    ],
    columns: [
      {
        key: 'reference',
        label: 'Registration',
        render: (item) => (
          <span className="font-mono font-bold">{item.reference}</span>
        ),
      },
      {
        key: 'tradingName',
        label: 'Trading name',
        render: (item) => text(item, 'tradingName'),
      },
      {
        key: 'legalName',
        label: 'Legal name',
        render: (item) => text(item, 'legalName'),
      },
      statusColumn,
    ],
  },
  locations: {
    title: 'Locations',
    singular: 'location',
    description: 'Customer sites, depots, ports and operational geofences.',
    viewPermission: 'Customer.View',
    managePermission: 'Customer.Manage',
    sortOptions: [
      { value: 'name', label: 'Name' },
      { value: 'reference', label: 'Code' },
      { value: 'type', label: 'Type' },
    ],
    columns: [
      {
        key: 'reference',
        label: 'Site code',
        render: (item) => (
          <span className="font-mono font-bold">{item.reference}</span>
        ),
      },
      { key: 'name', label: 'Name', render: (item) => text(item, 'name') },
      { key: 'type', label: 'Type', render: (item) => text(item, 'type') },
      {
        key: 'customerName',
        label: 'Customer',
        render: (item) => text(item, 'customerName'),
      },
      statusColumn,
    ],
  },
  commodities: {
    title: 'Commodities',
    singular: 'commodity',
    description: 'Transported goods and their default measurement units.',
    viewPermission: 'Contract.View',
    managePermission: 'Contract.Manage',
    sortOptions: [
      { value: 'name', label: 'Name' },
      { value: 'code', label: 'Code' },
    ],
    columns: [
      {
        key: 'reference',
        label: 'Code',
        render: (item) => (
          <span className="font-mono font-bold">{item.reference}</span>
        ),
      },
      { key: 'name', label: 'Name', render: (item) => text(item, 'name') },
      {
        key: 'defaultWeightUnit',
        label: 'Default unit',
        render: (item) => text(item, 'defaultWeightUnit'),
      },
      statusColumn,
    ],
  },
  drivers: {
    title: 'Drivers',
    singular: 'driver',
    description: 'Driver assignments, contacts and licence compliance.',
    viewPermission: 'Fleet.View',
    managePermission: 'Fleet.Manage',
    sortOptions: [
      { value: 'name', label: 'Name' },
      { value: 'reference', label: 'Licence' },
      { value: 'expiry', label: 'Licence expiry' },
    ],
    columns: [
      {
        key: 'name',
        label: 'Driver',
        render: (item) =>
          `${text(item, 'firstName')} ${text(item, 'lastName')}`,
      },
      {
        key: 'reference',
        label: 'Licence',
        render: (item) => (
          <span className="font-mono font-bold">{item.reference}</span>
        ),
      },
      {
        key: 'branchName',
        label: 'Branch',
        render: (item) => text(item, 'branchName'),
      },
      {
        key: 'licenceStatus',
        label: 'Licence',
        render: (item) => <Compliance value={text(item, 'licenceStatus')} />,
      },
      statusColumn,
    ],
  },
  vehicles: {
    title: 'Trucks',
    singular: 'truck',
    description: 'Truck capacity, availability and compliance status.',
    viewPermission: 'Fleet.View',
    managePermission: 'Fleet.Manage',
    sortOptions: [
      { value: 'fleetNumber', label: 'Fleet number' },
      { value: 'registration', label: 'Registration' },
      { value: 'expiry', label: 'Expiry' },
    ],
    columns: fleetColumns(),
  },
  trailers: {
    title: 'Trailers',
    singular: 'trailer',
    description: 'Trailer capacity, availability and licence status.',
    viewPermission: 'Fleet.View',
    managePermission: 'Fleet.Manage',
    sortOptions: [
      { value: 'fleetNumber', label: 'Fleet number' },
      { value: 'registration', label: 'Registration' },
      { value: 'expiry', label: 'Expiry' },
    ],
    columns: fleetColumns(),
  },
}

function fleetColumns(): DataColumn[] {
  return [
    {
      key: 'reference',
      label: 'Fleet no.',
      render: (item) => (
        <span className="font-mono font-bold">{item.reference}</span>
      ),
    },
    {
      key: 'registration',
      label: 'Registration',
      render: (item) => text(item, 'registration'),
    },
    {
      key: 'vehicle',
      label: 'Make / model',
      render: (item) => `${text(item, 'make')} ${text(item, 'model')}`,
    },
    {
      key: 'availability',
      label: 'Availability',
      render: (item) => (item.isAvailable ? 'Available' : 'Unavailable'),
    },
    {
      key: 'compliance',
      label: 'Compliance',
      render: (item) => <Compliance value={text(item, 'complianceStatus')} />,
    },
    statusColumn,
  ]
}

function Compliance({ value }: { value: string }) {
  const status = value
  return (
    <span
      className={
        status === 'Valid'
          ? 'font-bold text-emerald-700'
          : status === 'Expiring'
            ? 'font-bold text-amber-700'
            : 'font-bold text-red-700'
      }
    >
      {status}
    </span>
  )
}

type FormValues = Record<string, string | boolean>
const formBoundary = z.record(z.string(), z.union([z.string(), z.boolean()]))

const masterFieldHelp: Record<string, string> = {
  name: 'The clear operating name people will recognise in lists and searches.',
  code: 'A short unique reference used on documents, searches and integrations.',
  legalName: 'The customer name registered with the relevant authority.',
  tradingName:
    'The everyday customer name shown to dispatch and finance users.',
  registrationNumber: 'The customer company or business registration number.',
  taxNumber: 'The tax or VAT number used on finance documents when applicable.',
  paymentTermsDays: 'The number of days the customer has to pay an invoice.',
  contactName: 'The main person to contact about this record.',
  contactEmail:
    'The email address used for operational or account communication.',
  contactPhoneNumber:
    'The telephone number used for urgent operational contact.',
  contactCategory:
    'Identifies whether this person handles operations or finance.',
  financeContactName:
    'The person who handles customer invoices and account queries.',
  financeContactEmail: 'The address used for invoices and finance queries.',
  financeContactPhoneNumber: 'The direct number for customer finance queries.',
  notes: 'Internal context that other authorised users should know.',
  addressLine1: 'The primary street, site or physical address.',
  addressLine2: 'Optional additional building, unit or area information.',
  city: 'The town or city used for routing and correspondence.',
  provinceOrRegion: 'The province, state or operating region for this address.',
  postalCode: 'The postal code associated with the address.',
  countryCode: 'The two-letter country code, for example ZA for South Africa.',
  defaultWeightUnit:
    'The unit normally used when planning loads for this commodity.',
  type: 'Describes how this site is used in transport operations.',
  customerId:
    'Optionally links the location to the customer that owns or uses it.',
  latitude: 'The north-south GPS coordinate used for mapping and geofencing.',
  longitude: 'The east-west GPS coordinate used for mapping and geofencing.',
  geofenceRadiusMetres:
    'How close a device must be to count as arriving at the site.',
  operatingHours: 'The normal times during which vehicles may enter or load.',
  siteInstructions:
    'Standing access, safety, loading or delivery instructions.',
  branchId: 'The operating branch responsible for this driver or fleet asset.',
  driverType: 'Shows whether the driver is an employee or a contractor.',
  firstName: 'The driver’s given name as used in operational records.',
  lastName: 'The driver’s family name as used in operational records.',
  identificationReference:
    'An internal reference to the protected identity record; do not enter an ID image.',
  mobileNumber: 'The number used to contact the driver during a trip.',
  licenceNumber: 'The number printed on the driver or vehicle licence.',
  licenceExpiry: 'The last date on which the licence remains valid.',
  emergencyContactName:
    'The person to contact if an incident involves this driver.',
  emergencyContactNumber: 'The emergency contact’s telephone number.',
  fleetNumber:
    'The operator’s unique internal number for this truck or trailer.',
  registration: 'The official vehicle registration shown on the number plate.',
  make: 'The vehicle or trailer manufacturer.',
  model: 'The manufacturer’s model name or designation.',
  year: 'The model or manufacture year used for fleet records.',
  tareWeight: 'The empty weight of the truck or trailer, excluding cargo.',
  carryingCapacity: 'The maximum cargo weight this asset may carry.',
  weightUnit: 'The unit used for tare weight and carrying capacity.',
  roadworthyExpiry: 'The last date of the current roadworthy certificate.',
  isAvailable: 'Select this only when the asset may be allocated to a trip.',
}

const masterHelp = (name: string) =>
  masterFieldHelp[name] ??
  'Enter the value used to identify this record in operations.'

export function MasterDataPage({ module }: { module: MasterDataModule }) {
  const definition = definitions[module]
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('active')
  const [sort, setSort] = useState(
    definition.sortOptions.at(0)?.value ?? 'name',
  )
  const [direction, setDirection] = useState('asc')
  const [branchId, setBranchId] = useState('')
  const [customerId, setCustomerId] = useState('')
  const [editor, setEditor] = useState<MasterDataItem | 'new' | null>(null)
  const canManage = auth.user?.permissions.includes(definition.managePermission)
  const isFleetModule = ['drivers', 'vehicles', 'trailers'].includes(module)
  const filterBranches = useQuery({
    queryKey: ['master-data', 'branch-filter'],
    queryFn: () =>
      listMasterData(auth.request, 'branches', {
        page: 1,
        pageSize: 100,
        search: '',
        status: 'active',
        sort: 'name',
        direction: 'asc',
      }),
    enabled: isFleetModule,
  })
  const filterCustomers = useQuery({
    queryKey: ['master-data', 'customer-filter'],
    queryFn: () =>
      listMasterData(auth.request, 'customers', {
        page: 1,
        pageSize: 100,
        search: '',
        status: 'active',
        sort: 'tradingName',
        direction: 'asc',
      }),
    enabled: module === 'locations',
  })

  const list = useQuery({
    queryKey: [
      'master-data',
      module,
      page,
      search,
      status,
      sort,
      direction,
      branchId,
      customerId,
    ],
    queryFn: () =>
      listMasterData(auth.request, module, {
        page,
        pageSize: 10,
        search,
        status,
        sort,
        direction,
        ...(branchId ? { branchId } : {}),
        ...(customerId ? { customerId } : {}),
      }),
  })

  const statusMutation = useMutation({
    mutationFn: (item: MasterDataItem) =>
      setMasterDataStatus(auth.request, module, item),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['master-data', module] }),
  })

  if (!auth.user?.permissions.includes(definition.viewPermission)) {
    return (
      <section className="rounded-xl border bg-white p-8">
        <h1 className="text-2xl font-black">Access denied</h1>
        <p className="mt-2 text-slate-600">
          You do not have permission to view {definition.title.toLowerCase()}.
        </p>
      </section>
    )
  }

  return (
    <section>
      <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-bold tracking-[0.16em] text-emerald-700 uppercase">
            Master data
          </p>
          <h1 className="mt-1 text-3xl font-black text-slate-950">
            {definition.title}
          </h1>
          <p className="mt-1 text-slate-600">{definition.description}</p>
        </div>
        {canManage ? (
          <Button
            onClick={() => {
              setEditor('new')
            }}
            type="button"
          >
            <Plus aria-hidden="true" size={18} /> Add {definition.singular}
          </Button>
        ) : null}
      </div>

      <div className="mb-4 flex flex-wrap gap-3 rounded-xl border bg-white p-4">
        <label className="relative block">
          <span className="sr-only">Search</span>
          <Search
            aria-hidden="true"
            className="absolute top-3 left-3 text-slate-400"
            size={18}
          />
          <input
            className="h-11 min-w-64 rounded-md border py-2 pr-3 pl-10"
            onChange={(event) => {
              setPage(1)
              setSearch(event.target.value)
            }}
            placeholder={`Search ${definition.title.toLowerCase()}`}
            type="search"
            value={search}
          />
        </label>
        {isFleetModule ? (
          <label>
            <span className="sr-only">Branch filter</span>
            <select
              aria-label="Branch filter"
              className="h-11 rounded-md border bg-white px-3"
              onChange={(event) => {
                setPage(1)
                setBranchId(event.target.value)
              }}
              value={branchId}
            >
              <option value="">All branches</option>
              {filterBranches.data?.items.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {text(branch, 'name')}
                </option>
              ))}
            </select>
          </label>
        ) : null}
        {module === 'locations' ? (
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
              {filterCustomers.data?.items.map((customer) => (
                <option key={customer.id} value={customer.id}>
                  {text(customer, 'tradingName')}
                </option>
              ))}
            </select>
          </label>
        ) : null}
        <label>
          <span className="sr-only">Status</span>
          <select
            aria-label="Status"
            className="h-11 rounded-md border bg-white px-3"
            onChange={(event) => {
              setPage(1)
              setStatus(event.target.value)
            }}
            value={status}
          >
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
            <option value="all">All statuses</option>
          </select>
        </label>
        <label>
          <span className="sr-only">Sort by</span>
          <select
            aria-label="Sort by"
            className="h-11 rounded-md border bg-white px-3"
            onChange={(event) => {
              setSort(event.target.value)
            }}
            value={sort}
          >
            {definition.sortOptions.map((option) => (
              <option key={option.value} value={option.value}>
                Sort: {option.label}
              </option>
            ))}
          </select>
        </label>
        <Button
          onClick={() => {
            setDirection((value) => (value === 'asc' ? 'desc' : 'asc'))
          }}
          type="button"
          variant="outline"
        >
          {direction === 'asc' ? 'Ascending' : 'Descending'}
        </Button>
      </div>

      {list.isError ? (
        <div
          role="alert"
          className="mb-4 rounded-lg border border-red-200 bg-red-50 p-4 text-red-800"
        >
          {list.error.message}{' '}
          <button
            className="font-bold underline"
            onClick={() => void list.refetch()}
          >
            Try again
          </button>
        </div>
      ) : null}
      {statusMutation.isError ? (
        <p role="alert" className="mb-4 text-sm font-bold text-red-700">
          {statusMutation.error.message}
        </p>
      ) : null}

      <DataTable
        columns={definition.columns}
        data={list.data}
        loading={list.isLoading}
        onEdit={setEditor}
        onPageChange={setPage}
      />

      {editor ? (
        <RecordEditor
          definition={definition}
          item={editor === 'new' ? undefined : editor}
          module={module}
          onClose={() => {
            setEditor(null)
          }}
          onSaved={() => {
            setEditor(null)
            void queryClient.invalidateQueries({
              queryKey: ['master-data', module],
            })
          }}
          onStatus={async (item) => {
            if (
              window.confirm(
                `${item.isActive ? 'Deactivate' : 'Activate'} this ${definition.singular}?`,
              )
            ) {
              await statusMutation.mutateAsync(item)
              setEditor(null)
            }
          }}
        />
      ) : null}
    </section>
  )
}

interface EditorProps {
  definition: ModuleDefinition
  module: MasterDataModule
  item: MasterDataItem | undefined
  onClose: () => void
  onSaved: () => void
  onStatus: (item: MasterDataItem) => Promise<void>
}

function RecordEditor({
  definition,
  module,
  item,
  onClose,
  onSaved,
  onStatus,
}: EditorProps) {
  const auth = useAuth()
  const [formError, setFormError] = useState('')
  const defaults = useMemo(() => flatten(item), [item])
  const {
    register,
    handleSubmit,
    formState: { isDirty, errors },
  } = useForm<FormValues>({ defaultValues: defaults })
  const lookups = useLookups(module)

  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => {
      if (isDirty) event.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => {
      window.removeEventListener('beforeunload', warn)
    }
  }, [isDirty])

  const close = () => {
    if (!isDirty || window.confirm('Discard unsaved changes?')) onClose()
  }

  const save = useMutation({
    mutationFn: (values: FormValues) => {
      const parsed = formBoundary.parse(values)
      return saveMasterData(
        auth.request,
        module,
        buildPayload(module, parsed, item),
        item?.id,
      )
    },
    onSuccess: onSaved,
    onError: (error) => {
      setFormError(error.message)
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/45">
      <button
        aria-label="Close editor"
        className="absolute inset-0"
        onClick={close}
        type="button"
      />
      <aside
        aria-labelledby="editor-title"
        aria-modal="true"
        className="relative h-full w-full max-w-2xl overflow-y-auto bg-white shadow-2xl"
        role="dialog"
      >
        <div className="sticky top-0 z-10 flex items-start justify-between border-b bg-white px-6 py-5">
          <div>
            <p className="text-xs font-bold tracking-wider text-emerald-700 uppercase">
              {item ? 'Details and edit' : 'Create record'}
            </p>
            <h2 className="text-2xl font-black" id="editor-title">
              {item ? item.reference : `New ${definition.singular}`}
            </h2>
          </div>
          <Button
            aria-label="Close"
            onClick={close}
            type="button"
            variant="ghost"
          >
            <X aria-hidden="true" />
          </Button>
        </div>
        <form
          className="space-y-6 p-6"
          onSubmit={(event) => {
            void handleSubmit(
              (values) => {
                save.mutate(values)
              },
              (validationErrors) => {
                const first = Object.values(validationErrors).at(0)
                setFormError(
                  typeof first?.message === 'string'
                    ? first.message
                    : 'Review the highlighted fields.',
                )
              },
            )(event)
          }}
        >
          <ModuleFields
            errors={errors}
            lookups={lookups}
            module={module}
            register={register}
          />
          {formError ? (
            <p
              role="alert"
              className="rounded-lg bg-red-50 p-3 text-sm font-bold text-red-800"
            >
              {formError}
            </p>
          ) : null}
          <div className="flex flex-wrap justify-between gap-3 border-t pt-5">
            <div>
              {item ? (
                <Button
                  onClick={() => {
                    void onStatus(item)
                  }}
                  type="button"
                  variant="outline"
                >
                  {item.isActive ? 'Deactivate' : 'Activate'}
                </Button>
              ) : null}
            </div>
            <div className="flex gap-3">
              <Button onClick={close} type="button" variant="outline">
                Cancel
              </Button>
              <Button disabled={save.isPending} type="submit">
                {save.isPending ? 'Saving…' : item ? 'Save changes' : 'Create'}
              </Button>
            </div>
          </div>
        </form>
      </aside>
    </div>
  )
}

type Register = ReturnType<typeof useForm<FormValues>>['register']
type FormErrors = ReturnType<typeof useForm<FormValues>>['formState']['errors']

function ModuleFields({
  module,
  register,
  errors,
  lookups,
}: {
  module: MasterDataModule
  register: Register
  errors: FormErrors
  lookups: { branches: MasterDataItem[]; customers: MasterDataItem[] }
}) {
  const field = (
    name: string,
    label: string,
    options: { type?: string; required?: boolean } = {},
  ) => (
    <div className="block">
      <FieldLabel help={masterHelp(name)} label={label} />
      <input
        aria-label={label}
        className="h-11 w-full rounded-md border px-3"
        type={options.type ?? 'text'}
        {...register(name, {
          required:
            options.required === false ? false : `${label} is required.`,
        })}
      />
      {errors[name] ? (
        <span className="text-xs font-bold text-red-700">
          {String(errors[name].message)}
        </span>
      ) : null}
    </div>
  )

  const select = (name: string, label: string, options: string[]) => (
    <div className="block">
      <div>
        <FieldLabel help={masterHelp(name)} label={label} />
      </div>
      <select
        aria-label={label}
        className="h-11 w-full rounded-md border bg-white px-3"
        id={`master-data-${name}`}
        {...register(name, { required: `${label} is required.` })}
      >
        <option value="">Select {label.toLowerCase()}</option>
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </div>
  )

  const branchSelect = () => (
    <div className="block">
      <div>
        <FieldLabel help={masterHelp('branchId')} label="Branch" />
      </div>
      <select
        aria-label="Branch"
        className="h-11 w-full rounded-md border bg-white px-3"
        id="master-data-branch"
        {...register('branchId', { required: 'Branch is required.' })}
      >
        <option value="">Select branch</option>
        {lookups.branches.map((branch) => (
          <option key={branch.id} value={branch.id}>
            {text(branch, 'name')}
          </option>
        ))}
      </select>
    </div>
  )

  if (module === 'commodities') {
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        {field('name', 'Commodity name')}
        {field('code', 'Commodity code')}
        {select('defaultWeightUnit', 'Weight unit', ['Tonnes', 'Kilograms'])}
      </div>
    )
  }

  if (module === 'customers') {
    return (
      <>
        <fieldset className="grid gap-4 sm:grid-cols-2">
          <legend className="mb-3 text-lg font-black">Legal and billing</legend>
          {field('legalName', 'Legal name')}
          {field('tradingName', 'Trading name')}
          {field('registrationNumber', 'Registration number')}
          {field('taxNumber', 'Tax number', { required: false })}
          {field('paymentTermsDays', 'Payment terms (days)', {
            type: 'number',
          })}
        </fieldset>
        <AddressFields field={field} />
        <fieldset className="grid gap-4 sm:grid-cols-2">
          <legend className="mb-3 text-lg font-black">Primary contact</legend>
          {field('contactName', 'Contact name')}
          {field('contactEmail', 'Contact email', { type: 'email' })}
          {field('contactPhoneNumber', 'Contact phone', { required: false })}
          {select('contactCategory', 'Contact category', [
            'Operational',
            'Finance',
          ])}
        </fieldset>
        <fieldset className="grid gap-4 sm:grid-cols-2">
          <legend className="mb-3 text-lg font-black">
            Finance contact (optional)
          </legend>
          {field('financeContactName', 'Finance contact name', {
            required: false,
          })}
          {field('financeContactEmail', 'Finance contact email', {
            type: 'email',
            required: false,
          })}
          {field('financeContactPhoneNumber', 'Finance contact phone', {
            required: false,
          })}
        </fieldset>
        <div className="block">
          <FieldLabel help={masterHelp('notes')} label="Notes" />
          <textarea
            aria-label="Notes"
            className="min-h-24 w-full rounded-md border p-3"
            {...register('notes')}
          />
        </div>
      </>
    )
  }

  if (module === 'locations') {
    return (
      <>
        <div className="grid gap-4 sm:grid-cols-2">
          {field('name', 'Location name')}
          {field('code', 'Location code')}
          {select('type', 'Location type', [
            'Customer',
            'Mine',
            'Depot',
            'Weighbridge',
            'Warehouse',
            'Port',
            'DeliverySite',
            'Generic',
          ])}
          <div className="block">
            <FieldLabel help={masterHelp('customerId')} label="Customer" />
            <select
              aria-label="Customer"
              className="h-11 w-full rounded-md border bg-white px-3"
              {...register('customerId')}
            >
              <option value="">No customer</option>
              {lookups.customers.map((customer) => (
                <option key={customer.id} value={customer.id}>
                  {text(customer, 'tradingName')}
                </option>
              ))}
            </select>
          </div>
        </div>
        <AddressFields field={field} />
        <fieldset className="grid gap-4 sm:grid-cols-2">
          <legend className="mb-3 text-lg font-black">Site operations</legend>
          {field('latitude', 'Latitude', { type: 'number', required: false })}
          {field('longitude', 'Longitude', { type: 'number', required: false })}
          {field('geofenceRadiusMetres', 'Geofence radius (m)', {
            type: 'number',
            required: false,
          })}
          {field('operatingHours', 'Operating hours', { required: false })}
          {field('contactName', 'Site contact', { required: false })}
          {field('contactPhoneNumber', 'Site contact phone', {
            required: false,
          })}
        </fieldset>
        <div className="block">
          <FieldLabel
            help={masterHelp('siteInstructions')}
            label="Site instructions"
          />
          <textarea
            aria-label="Site instructions"
            className="min-h-24 w-full rounded-md border p-3"
            {...register('siteInstructions')}
          />
        </div>
      </>
    )
  }

  if (module === 'branches') {
    return (
      <>
        <div className="grid gap-4 sm:grid-cols-2">
          {field('name', 'Branch name')}
          {field('code', 'Branch code')}
          {field('contactEmail', 'Contact email', {
            type: 'email',
            required: false,
          })}
          {field('contactPhoneNumber', 'Contact phone', { required: false })}
        </div>
        <AddressFields field={field} />
      </>
    )
  }

  if (module === 'drivers') {
    return (
      <div className="grid gap-4 sm:grid-cols-2">
        {branchSelect()}
        {select('driverType', 'Driver type', ['Employee', 'Contractor'])}
        {field('firstName', 'First name')}
        {field('lastName', 'Last name')}
        {field('identificationReference', 'Internal identity reference')}
        {field('mobileNumber', 'Mobile number')}
        {field('licenceNumber', 'Licence number')}
        {field('licenceExpiry', 'Licence expiry', { type: 'date' })}
        {field('emergencyContactName', 'Emergency contact')}
        {field('emergencyContactNumber', 'Emergency contact phone')}
      </div>
    )
  }

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {branchSelect()}
      {field('fleetNumber', 'Fleet number')}
      {field('registration', 'Registration')}
      {field('make', 'Make')}
      {field('model', 'Model')}
      {field('year', 'Year', { type: 'number' })}
      {field('tareWeight', 'Tare weight', { type: 'number' })}
      {field('carryingCapacity', 'Carrying capacity', { type: 'number' })}
      {select('weightUnit', 'Weight unit', ['Tonnes', 'Kilograms'])}
      {module === 'vehicles'
        ? field('roadworthyExpiry', 'Roadworthy expiry', { type: 'date' })
        : null}
      {field('licenceExpiry', 'Licence expiry', { type: 'date' })}
      <div className="flex items-center gap-3 pt-7 font-bold">
        <input
          aria-label="Currently available"
          type="checkbox"
          {...register('isAvailable')}
        />
        Currently available
        <FieldHelp
          label="Currently available"
          text={masterHelp('isAvailable')}
        />
      </div>
    </div>
  )
}

function AddressFields({
  field,
}: {
  field: (
    name: string,
    label: string,
    options?: { type?: string; required?: boolean },
  ) => ReactNode
}) {
  return (
    <fieldset className="grid gap-4 sm:grid-cols-2">
      <legend className="mb-3 text-lg font-black">Address</legend>
      {field('addressLine1', 'Address line 1')}
      {field('addressLine2', 'Address line 2', { required: false })}
      {field('city', 'City')}
      {field('provinceOrRegion', 'Province / region')}
      {field('postalCode', 'Postal code')}
      {field('countryCode', 'Country code')}
    </fieldset>
  )
}

function useLookups(module: MasterDataModule) {
  const auth = useAuth()
  const needsBranches = ['drivers', 'vehicles', 'trailers'].includes(module)
  const needsCustomers = module === 'locations'
  const branches = useQuery({
    queryKey: ['master-data', 'branch-lookup'],
    queryFn: () =>
      listMasterData(auth.request, 'branches', {
        page: 1,
        pageSize: 100,
        search: '',
        status: 'active',
        sort: 'name',
        direction: 'asc',
      }),
    enabled: needsBranches,
  })
  const customers = useQuery({
    queryKey: ['master-data', 'customer-lookup'],
    queryFn: () =>
      listMasterData(auth.request, 'customers', {
        page: 1,
        pageSize: 100,
        search: '',
        status: 'active',
        sort: 'tradingName',
        direction: 'asc',
      }),
    enabled: needsCustomers,
  })
  return {
    branches: branches.data?.items ?? [],
    customers: customers.data?.items ?? [],
  }
}

function flatten(item?: MasterDataItem): FormValues {
  if (!item) {
    return {
      countryCode: 'ZA',
      paymentTermsDays: '30',
      weightUnit: 'Tonnes',
      driverType: 'Employee',
      contactCategory: 'Operational',
      isAvailable: true,
      licenceExpiry: '2027-12-31',
      roadworthyExpiry: '2027-12-31',
      year: String(new Date().getFullYear()),
    }
  }
  const address =
    (item.address as Record<string, unknown> | undefined) ??
    (item.billingAddress as Record<string, unknown> | undefined) ??
    {}
  const contacts =
    (item.contacts as Record<string, unknown>[] | undefined) ?? []
  const contact = contacts[0] ?? {}
  const financeContact =
    contacts.find((candidate) => candidate.category === 'Finance') ?? {}
  const scalar = (value: unknown, fallback = '') =>
    typeof value === 'string' || typeof value === 'number'
      ? String(value)
      : fallback
  const values: FormValues = {}
  for (const [key, value] of Object.entries(item)) {
    if (typeof value === 'string' || typeof value === 'boolean')
      values[key] = value
    if (typeof value === 'number') values[key] = String(value)
  }
  return {
    ...values,
    code: scalar(item.code ?? item.reference),
    fleetNumber: scalar(item.fleetNumber ?? item.reference),
    addressLine1: scalar(address.line1),
    addressLine2: scalar(address.line2),
    city: scalar(address.city),
    provinceOrRegion: scalar(address.provinceOrRegion),
    postalCode: scalar(address.postalCode),
    countryCode: scalar(address.countryCode, 'ZA'),
    contactName: scalar(contact.name ?? item.contactName),
    contactEmail: scalar(contact.email ?? item.contactEmail),
    contactPhoneNumber: scalar(contact.phoneNumber ?? item.contactPhoneNumber),
    contactCategory: scalar(contact.category, 'Operational'),
    financeContactName: scalar(financeContact.name),
    financeContactEmail: scalar(financeContact.email),
    financeContactPhoneNumber: scalar(financeContact.phoneNumber),
  }
}

function buildPayload(
  module: MasterDataModule,
  values: FormValues,
  item?: MasterDataItem,
) {
  const value = (key: string) => String(values[key] ?? '').trim()
  const optional = (key: string) => value(key) || null
  const numeric = (key: string) => Number(value(key))
  const address = {
    line1: value('addressLine1'),
    line2: optional('addressLine2'),
    city: value('city'),
    provinceOrRegion: value('provinceOrRegion'),
    postalCode: value('postalCode'),
    countryCode: value('countryCode').toUpperCase(),
  }
  const version = item?.version
  switch (module) {
    case 'branches':
      return {
        name: value('name'),
        code: value('code'),
        address,
        contactEmail: optional('contactEmail'),
        contactPhoneNumber: optional('contactPhoneNumber'),
        version,
      }
    case 'customers': {
      const contacts =
        (item?.contacts as Record<string, unknown>[] | undefined) ?? []
      const primaryContact = contacts[0]
      const financeContact = contacts.find(
        (candidate) => candidate.category === 'Finance',
      )

      return {
        legalName: value('legalName'),
        tradingName: value('tradingName'),
        registrationNumber: value('registrationNumber'),
        taxNumber: optional('taxNumber'),
        paymentTermsDays: numeric('paymentTermsDays'),
        billingAddress: address,
        contacts: [
          {
            id: (primaryContact?.id as string | undefined) ?? null,
            name: value('contactName'),
            email: optional('contactEmail'),
            phoneNumber: optional('contactPhoneNumber'),
            category: value('contactCategory'),
            isActive: true,
          },
          ...(value('financeContactName')
            ? [
                {
                  id: (financeContact?.id as string | undefined) ?? null,
                  name: value('financeContactName'),
                  email: optional('financeContactEmail'),
                  phoneNumber: optional('financeContactPhoneNumber'),
                  category: 'Finance',
                  isActive: true,
                },
              ]
            : []),
        ],
        notes: optional('notes'),
        version,
      }
    }
    case 'locations':
      return {
        customerId: optional('customerId'),
        name: value('name'),
        code: value('code'),
        type: value('type'),
        address,
        latitude: optional('latitude') ? numeric('latitude') : null,
        longitude: optional('longitude') ? numeric('longitude') : null,
        geofenceRadiusMetres: optional('geofenceRadiusMetres')
          ? numeric('geofenceRadiusMetres')
          : null,
        siteInstructions: optional('siteInstructions'),
        contactName: optional('contactName'),
        contactPhoneNumber: optional('contactPhoneNumber'),
        operatingHours: optional('operatingHours'),
        version,
      }
    case 'commodities':
      return {
        name: value('name'),
        code: value('code'),
        defaultWeightUnit: value('defaultWeightUnit'),
        version,
      }
    case 'drivers':
      return {
        branchId: value('branchId'),
        firstName: value('firstName'),
        lastName: value('lastName'),
        driverType: value('driverType'),
        identificationReference: value('identificationReference'),
        mobileNumber: value('mobileNumber'),
        licenceNumber: value('licenceNumber'),
        licenceExpiry: value('licenceExpiry'),
        emergencyContactName: value('emergencyContactName'),
        emergencyContactNumber: value('emergencyContactNumber'),
        version,
      }
    case 'vehicles':
      return {
        branchId: value('branchId'),
        registration: value('registration'),
        fleetNumber: value('fleetNumber'),
        make: value('make'),
        model: value('model'),
        year: numeric('year'),
        tareWeight: numeric('tareWeight'),
        carryingCapacity: numeric('carryingCapacity'),
        weightUnit: value('weightUnit'),
        roadworthyExpiry: value('roadworthyExpiry'),
        licenceExpiry: value('licenceExpiry'),
        isAvailable: Boolean(values.isAvailable),
        version,
      }
    case 'trailers':
      return {
        branchId: value('branchId'),
        registration: value('registration'),
        fleetNumber: value('fleetNumber'),
        make: value('make'),
        model: value('model'),
        year: numeric('year'),
        tareWeight: numeric('tareWeight'),
        carryingCapacity: numeric('carryingCapacity'),
        weightUnit: value('weightUnit'),
        licenceExpiry: value('licenceExpiry'),
        isAvailable: Boolean(values.isAvailable),
        version,
      }
  }
}
