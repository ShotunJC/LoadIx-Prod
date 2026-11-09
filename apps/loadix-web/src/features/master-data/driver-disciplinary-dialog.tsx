import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  CheckCircle2,
  DollarSign,
  Plus,
  ShieldAlert,
  X,
} from 'lucide-react'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  getDriverDisciplinarySummary,
  issueWarning,
  revokeWarning,
  createDeduction,
  approveDeduction,
  cancelDeduction,
  WARNING_TYPE_LABELS,
  WARNING_CATEGORY_LABELS,
  DEDUCTION_CATEGORY_LABELS,
} from './driver-disciplinary-api'

interface DriverDisciplinaryDialogProps {
  driverId: string
  driverName: string
  employeeNumber?: string | null | undefined
  onClose: () => void
}

export function DriverDisciplinaryDialog({
  driverId,
  driverName,
  employeeNumber,
  onClose,
}: DriverDisciplinaryDialogProps) {
  const auth = useAuth()
  const queryClient = useQueryClient()
  const [activeTab, setActiveTab] = useState<'warnings' | 'deductions'>('warnings')
  const [showIssueWarning, setShowIssueWarning] = useState(false)
  const [showCreateDeduction, setShowCreateDeduction] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  // Warning Form State
  const [warningType, setWarningType] = useState(2) // FirstWritten
  const [warningCategory, setWarningCategory] = useState(1) // Speeding
  const [warningNumber, setWarningNumber] = useState(
    () =>
      `WRN-${String(new Date().getFullYear())}-${String(Date.now()).slice(-4)}`,
  )
  const [warningDesc, setWarningDesc] = useState('')
  const [incidentDate, setIncidentDate] = useState(
    new Date().toISOString().slice(0, 10),
  )

  // Deduction Form State
  const [deductionCategory, setDeductionCategory] = useState(1) // TrafficFine
  const [deductionNumber, setDeductionNumber] = useState(
    () =>
      `DED-${String(new Date().getFullYear())}-${String(Date.now()).slice(-4)}`,
  )
  const [totalAmount, setTotalAmount] = useState('1000')
  const [installmentAmount, setInstallmentAmount] = useState('500')
  const [deductionDesc, setDeductionDesc] = useState('')
  const [effectiveDate, setEffectiveDate] = useState(
    new Date().toISOString().slice(0, 10),
  )

  const summaryQuery = useQuery({
    queryKey: ['driver-disciplinary-summary', driverId],
    queryFn: () => getDriverDisciplinarySummary(auth.request, driverId),
  })

  const issueWarningMutation = useMutation({
    mutationFn: () => {
      const issued = new Date()
      const expiry = new Date()
      expiry.setMonth(expiry.getMonth() + 6)
      return issueWarning(auth.request, driverId, {
        warningNumber,
        warningType,
        category: warningCategory,
        incidentDateUtc: new Date(incidentDate).toISOString(),
        issuedDateUtc: issued.toISOString(),
        expiryDateUtc: expiry.toISOString(),
        description: warningDesc,
      })
    },
    onSuccess: () => {
      setShowIssueWarning(false)
      setWarningDesc('')
      setActionError(null)
      void queryClient.invalidateQueries({
        queryKey: ['driver-disciplinary-summary', driverId],
      })
    },
    onError: (err: Error) => {
      setActionError(err.message)
    },
  })

  const revokeWarningMutation = useMutation({
    mutationFn: (warningId: string) => {
      const reason = window.prompt(
        'Please state the justification for revoking this warning:',
      )
      if (!reason || !reason.trim())
        throw new Error('Revocation reason is required.')
      return revokeWarning(auth.request, warningId, reason.trim())
    },
    onSuccess: () => {
      setActionError(null)
      void queryClient.invalidateQueries({
        queryKey: ['driver-disciplinary-summary', driverId],
      })
    },
    onError: (err: Error) => {
      setActionError(err.message)
    },
  })

  const createDeductionMutation = useMutation({
    mutationFn: () => {
      return createDeduction(auth.request, driverId, {
        deductionNumber,
        category: deductionCategory,
        currencyCode: 'ZAR',
        totalAmount: parseFloat(totalAmount),
        installmentAmount: parseFloat(installmentAmount),
        effectiveDate,
        description: deductionDesc,
      })
    },
    onSuccess: () => {
      setShowCreateDeduction(false)
      setDeductionDesc('')
      setActionError(null)
      void queryClient.invalidateQueries({
        queryKey: ['driver-disciplinary-summary', driverId],
      })
    },
    onError: (err: Error) => {
      setActionError(err.message)
    },
  })

  const approveDeductionMutation = useMutation({
    mutationFn: (deductionId: string) =>
      approveDeduction(auth.request, deductionId),
    onSuccess: () => {
      setActionError(null)
      void queryClient.invalidateQueries({
        queryKey: ['driver-disciplinary-summary', driverId],
      })
    },
    onError: (err: Error) => {
      setActionError(err.message)
    },
  })

  const cancelDeductionMutation = useMutation({
    mutationFn: (deductionId: string) => {
      const reason = window.prompt(
        'Please enter reason for cancelling this deduction:',
      )
      if (!reason || !reason.trim())
        throw new Error('Cancellation reason is required.')
      return cancelDeduction(auth.request, deductionId, reason.trim())
    },
    onSuccess: () => {
      setActionError(null)
      void queryClient.invalidateQueries({
        queryKey: ['driver-disciplinary-summary', driverId],
      })
    },
    onError: (err: Error) => {
      setActionError(err.message)
    },
  })

  const summary = summaryQuery.data

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/50 backdrop-blur-xs">
      <button
        aria-label="Close"
        className="absolute inset-0"
        onClick={() => {
          onClose()
        }}
        type="button"
      />
      <aside
        aria-labelledby="dialog-title"
        className="relative h-full w-full max-w-3xl overflow-y-auto bg-white shadow-2xl"
        role="dialog"
      >
        <div className="sticky top-0 z-10 flex items-start justify-between border-b bg-white px-6 py-5">
          <div>
            <div className="flex items-center gap-2">
              <ShieldAlert className="h-5 w-5 text-emerald-700" />
              <p className="text-xs font-bold tracking-wider text-emerald-700 uppercase">
                Driver Disciplinary & Payroll Recoveries
              </p>
            </div>
            <h2 className="text-2xl font-black text-slate-900" id="dialog-title">
              {driverName}
            </h2>
            {employeeNumber ? (
              <p className="text-xs text-slate-500">
                Payroll Code:{' '}
                <span className="font-mono font-bold text-slate-700">
                  {employeeNumber}
                </span>
              </p>
            ) : (
              <p className="text-xs text-amber-600">
                No payroll code assigned — please update driver master data to sync with Pastel Payroll.
              </p>
            )}
          </div>
          <Button
            aria-label="Close"
            onClick={() => {
              onClose()
            }}
            type="button"
            variant="ghost"
          >
            <X className="h-5 w-5" />
          </Button>
        </div>

        {actionError ? (
          <div className="m-6 rounded-lg border border-red-200 bg-red-50 p-4 text-sm font-semibold text-red-700">
            {actionError}
          </div>
        ) : null}

        {/* Summary Metric Cards */}
        <div className="grid grid-cols-3 gap-4 border-b bg-slate-50 p-6">
          <div className="rounded-lg border bg-white p-3 shadow-xs">
            <p className="text-xs font-medium text-slate-500">Active Warnings</p>
            <p className="text-2xl font-black text-amber-600">
              {summary ? summary.activeWarningsCount : '—'}
            </p>
            <p className="text-[11px] text-slate-400">
              Total lifetime: {summary?.totalWarningsCount ?? 0}
            </p>
          </div>
          <div className="rounded-lg border bg-white p-3 shadow-xs">
            <p className="text-xs font-medium text-slate-500">
              Active Deductions Balance
            </p>
            <p className="text-2xl font-black text-slate-900">
              R{' '}
              {summary
                ? summary.totalActiveDeductionsBalance.toLocaleString('en-ZA', {
                    minimumFractionDigits: 2,
                  })
                : '0.00'}
            </p>
            <p className="text-[11px] text-slate-400">
              Pending recovery via payroll
            </p>
          </div>
          <div className="rounded-lg border bg-white p-3 shadow-xs">
            <p className="text-xs font-medium text-slate-500">
              Statutory BCEA §34 Limit
            </p>
            <p className="text-sm font-bold text-emerald-700 mt-1">
              25% Max Pay Ceiling
            </p>
            <p className="text-[11px] text-slate-400">
              Enforced during payroll export
            </p>
          </div>
        </div>

        {/* Tab Buttons */}
        <div className="flex border-b px-6">
          <button
            className={`border-b-2 py-3 px-4 text-sm font-bold transition-colors ${
              activeTab === 'warnings'
                ? 'border-emerald-600 text-emerald-700'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
            onClick={() => {
              setActiveTab('warnings')
            }}
            type="button"
          >
            Disciplinary Warnings ({summary?.activeWarnings.length ?? 0})
          </button>
          <button
            className={`border-b-2 py-3 px-4 text-sm font-bold transition-colors ${
              activeTab === 'deductions'
                ? 'border-emerald-600 text-emerald-700'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
            onClick={() => {
              setActiveTab('deductions')
            }}
            type="button"
          >
            Deductions & Recoveries ({summary?.activeDeductions.length ?? 0})
          </button>
        </div>

        <div className="p-6">
          {activeTab === 'warnings' ? (
            <div className="space-y-6">
              <div className="flex items-center justify-between">
                <p className="text-xs font-semibold text-slate-500">
                  Manage progressive disciplinary action in accordance with SA labour standards.
                </p>
                <Button
                  className="min-h-8 px-3 text-xs"
                  onClick={() => {
                    setShowIssueWarning(!showIssueWarning)
                  }}
                  type="button"
                >
                  <Plus className="mr-1.5 h-4 w-4" />
                  {showIssueWarning ? 'Cancel' : 'Issue warning'}
                </Button>
              </div>

              {showIssueWarning ? (
                <div className="rounded-xl border border-emerald-200 bg-emerald-50/50 p-5 space-y-4">
                  <h3 className="font-bold text-slate-900">Issue Disciplinary Notice</h3>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div>
                      <label className="text-xs font-bold text-slate-700">Notice reference</label>
                      <input
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        onChange={(e) => {
                          setWarningNumber(e.target.value)
                        }}
                        value={warningNumber}
                      />
                    </div>
                    <div>
                      <label className="text-xs font-bold text-slate-700">Incident date</label>
                      <input
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        onChange={(e) => {
                          setIncidentDate(e.target.value)
                        }}
                        type="date"
                        value={incidentDate}
                      />
                    </div>
                    <div>
                      <label className="text-xs font-bold text-slate-700">Warning severity level</label>
                      <select
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        onChange={(e) => {
                          setWarningType(Number(e.target.value))
                        }}
                        value={warningType}
                      >
                        <option value={1}>Verbal Warning</option>
                        <option value={2}>First Written Warning</option>
                        <option value={3}>Second Written Warning</option>
                        <option value={4}>Final Written Warning</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-xs font-bold text-slate-700">Infraction category</label>
                      <select
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        onChange={(e) => {
                          setWarningCategory(Number(e.target.value))
                        }}
                        value={warningCategory}
                      >
                        {Object.entries(WARNING_CATEGORY_LABELS).map(([k, label]) => (
                          <option key={k} value={k}>{label}</option>
                        ))}
                      </select>
                    </div>
                  </div>
                  <div>
                    <label className="text-xs font-bold text-slate-700">Description / Details of incident</label>
                    <textarea
                      className="mt-1 w-full rounded border bg-white p-2 text-sm"
                      onChange={(e) => {
                        setWarningDesc(e.target.value)
                      }}
                      placeholder="Specify the circumstances, telemetry facts, location, or witness statements..."
                      rows={3}
                      value={warningDesc}
                    />
                  </div>
                  <div className="flex justify-end gap-2">
                    <Button
                      onClick={() => {
                        setShowIssueWarning(false)
                      }}
                      type="button"
                      variant="ghost"
                    >
                      Cancel
                    </Button>
                    <Button
                      disabled={!warningDesc.trim() || issueWarningMutation.isPending}
                      onClick={() => {
                        issueWarningMutation.mutate()
                      }}
                      type="button"
                    >
                      {issueWarningMutation.isPending ? 'Issuing...' : 'Save and issue notice'}
                    </Button>
                  </div>
                </div>
              ) : null}

              {/* Warnings List */}
              {summary?.activeWarnings.length === 0 ? (
                <div className="rounded-lg border border-dashed p-8 text-center text-slate-500">
                  <CheckCircle2 className="mx-auto h-8 w-8 text-emerald-500 opacity-60 mb-2" />
                  <p className="font-bold">No active warnings</p>
                  <p className="text-xs">Driver has a clean disciplinary record.</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {summary?.activeWarnings.map((warning) => (
                    <div
                      key={warning.id}
                      className="rounded-lg border bg-white p-4 shadow-xs transition hover:border-slate-300"
                    >
                      <div className="flex items-start justify-between">
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-mono text-xs font-bold text-slate-900">
                              {warning.warningNumber}
                            </span>
                            <span
                              className={`rounded-full px-2 py-0.5 text-xs font-bold ${
                                warning.warningType === 4
                                  ? 'bg-red-100 text-red-800'
                                  : warning.warningType === 3
                                    ? 'bg-orange-100 text-orange-800'
                                    : 'bg-amber-100 text-amber-800'
                              }`}
                            >
                              {WARNING_TYPE_LABELS[warning.warningType]}
                            </span>
                            <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">
                              {WARNING_CATEGORY_LABELS[warning.category]}
                            </span>
                          </div>
                          <p className="mt-2 text-sm text-slate-700">{warning.description}</p>
                          <div className="mt-2 flex gap-4 text-xs text-slate-400">
                            <span>Incident: {new Date(warning.incidentDateUtc).toLocaleDateString()}</span>
                            <span>Issued: {new Date(warning.issuedDateUtc).toLocaleDateString()}</span>
                            <span>Expires: {new Date(warning.expiryDateUtc).toLocaleDateString()}</span>
                          </div>
                        </div>
                        {warning.status === 1 ? (
                          <Button
                            className="min-h-8 px-2.5 text-xs text-red-600 hover:text-red-700"
                            onClick={() => {
                              revokeWarningMutation.mutate(warning.id)
                            }}
                            type="button"
                            variant="ghost"
                          >
                            Revoke
                          </Button>
                        ) : null}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          ) : (
            <div className="space-y-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs font-semibold text-slate-500">
                    BCEA §34 compliant driver deduction and installment schedules.
                  </p>
                </div>
                <Button
                  className="min-h-8 px-3 text-xs"
                  onClick={() => {
                    setShowCreateDeduction(!showCreateDeduction)
                  }}
                  type="button"
                >
                  <Plus className="mr-1.5 h-4 w-4" />
                  {showCreateDeduction ? 'Cancel' : 'Create deduction'}
                </Button>
              </div>

              {showCreateDeduction ? (
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-5 space-y-4">
                  <h3 className="font-bold text-slate-900">Schedule Recovery Deduction</h3>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div>
                      <label className="text-xs font-bold text-slate-700">Deduction reference</label>
                      <input
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        onChange={(e) => {
                          setDeductionNumber(e.target.value)
                        }}
                        value={deductionNumber}
                      />
                    </div>
                    <div>
                      <label className="text-xs font-bold text-slate-700">Category</label>
                      <select
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        onChange={(e) => {
                          setDeductionCategory(Number(e.target.value))
                        }}
                        value={deductionCategory}
                      >
                        {Object.entries(DEDUCTION_CATEGORY_LABELS).map(([k, label]) => (
                          <option key={k} value={k}>{label}</option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label className="text-xs font-bold text-slate-700">Total liability amount (ZAR)</label>
                      <input
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        min="1"
                        onChange={(e) => {
                          setTotalAmount(e.target.value)
                        }}
                        step="0.01"
                        type="number"
                        value={totalAmount}
                      />
                    </div>
                    <div>
                      <label className="text-xs font-bold text-slate-700">Installment per pay run (ZAR)</label>
                      <input
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        min="1"
                        onChange={(e) => {
                          setInstallmentAmount(e.target.value)
                        }}
                        step="0.01"
                        type="number"
                        value={installmentAmount}
                      />
                    </div>
                    <div>
                      <label className="text-xs font-bold text-slate-700">Effective start date</label>
                      <input
                        className="mt-1 w-full rounded border bg-white p-2 text-sm"
                        onChange={(e) => {
                          setEffectiveDate(e.target.value)
                        }}
                        type="date"
                        value={effectiveDate}
                      />
                    </div>
                  </div>
                  <div>
                    <label className="text-xs font-bold text-slate-700">Description / Settlement Agreement Reference</label>
                    <textarea
                      className="mt-1 w-full rounded border bg-white p-2 text-sm"
                      onChange={(e) => {
                        setDeductionDesc(e.target.value)
                      }}
                      placeholder="Reference signed agreement, traffic fine notice number, or accident claim..."
                      rows={2}
                      value={deductionDesc}
                    />
                  </div>
                  <div className="rounded bg-amber-50 p-3 text-xs text-amber-800 border border-amber-200">
                    <p className="font-bold">BCEA Compliance Note:</p>
                    <p>South African law requires employee written agreement for damage/loss recovery. Ensure signed consent is retained on file.</p>
                  </div>
                  <div className="flex justify-end gap-2">
                    <Button
                      onClick={() => {
                        setShowCreateDeduction(false)
                      }}
                      type="button"
                      variant="ghost"
                    >
                      Cancel
                    </Button>
                    <Button
                      disabled={!deductionDesc.trim() || createDeductionMutation.isPending}
                      onClick={() => {
                        createDeductionMutation.mutate()
                      }}
                      type="button"
                    >
                      {createDeductionMutation.isPending ? 'Saving...' : 'Create draft deduction'}
                    </Button>
                  </div>
                </div>
              ) : null}

              {/* Deductions List */}
              {summary?.activeDeductions.length === 0 ? (
                <div className="rounded-lg border border-dashed p-8 text-center text-slate-500">
                  <DollarSign className="mx-auto h-8 w-8 text-emerald-500 opacity-60 mb-2" />
                  <p className="font-bold">No active deductions</p>
                  <p className="text-xs">No pending recoveries against driver wages.</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {summary?.activeDeductions.map((deduction) => {
                    const recovered = deduction.totalAmount - deduction.remainingBalance
                    const progressPercent = Math.min(100, Math.round((recovered / deduction.totalAmount) * 100))

                    return (
                      <div
                        key={deduction.id}
                        className="rounded-lg border bg-white p-4 shadow-xs"
                      >
                        <div className="flex items-start justify-between">
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="font-mono text-xs font-bold text-slate-900">
                                {deduction.deductionNumber}
                              </span>
                              <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-700">
                                {DEDUCTION_CATEGORY_LABELS[deduction.category]}
                              </span>
                              <span
                                className={`rounded-full px-2 py-0.5 text-[11px] font-bold ${
                                  deduction.status === 2 || deduction.status === 3
                                    ? 'bg-emerald-100 text-emerald-800'
                                    : deduction.status === 1
                                      ? 'bg-amber-100 text-amber-800'
                                      : 'bg-slate-100 text-slate-700'
                                }`}
                              >
                                {deduction.status === 1 ? 'Draft' : deduction.status === 3 ? 'Active' : deduction.status === 4 ? 'Completed' : 'Cancelled'}
                              </span>
                            </div>
                            <p className="mt-1 text-sm text-slate-700">{deduction.description}</p>
                          </div>
                          <div className="text-right">
                            <p className="text-xs text-slate-400">Remaining</p>
                            <p className="text-base font-black text-slate-900">
                              R {deduction.remainingBalance.toFixed(2)}
                            </p>
                            <p className="text-[11px] text-slate-500">
                              Installment: R {deduction.installmentAmount.toFixed(2)} / run
                            </p>
                          </div>
                        </div>

                        {/* Progress Bar */}
                        <div className="mt-3">
                          <div className="flex justify-between text-[11px] text-slate-500 mb-1">
                            <span>Recovered: R {recovered.toFixed(2)}</span>
                            <span>Total: R {deduction.totalAmount.toFixed(2)} ({String(progressPercent)}%)</span>
                          </div>
                          <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
                            <div
                              className="h-full bg-emerald-600 transition-all duration-300"
                              style={{ width: `${String(progressPercent)}%` }}
                            />
                          </div>
                        </div>

                        {/* Action Buttons */}
                        <div className="mt-3 flex justify-end gap-2 border-t pt-2">
                          {deduction.status === 1 ? (
                            <Button
                              className="min-h-8 px-3 text-xs"
                              onClick={() => {
                                approveDeductionMutation.mutate(deduction.id)
                              }}
                              type="button"
                            >
                              Approve deduction
                            </Button>
                          ) : null}
                          {deduction.status === 3 ? (
                            <Button
                              className="min-h-8 px-3 text-xs text-red-600 hover:text-red-700"
                              onClick={() => {
                                cancelDeductionMutation.mutate(deduction.id)
                              }}
                              type="button"
                              variant="ghost"
                            >
                              Cancel deduction
                            </Button>
                          ) : null}
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}
            </div>
          )}
        </div>
      </aside>
    </div>
  )
}
