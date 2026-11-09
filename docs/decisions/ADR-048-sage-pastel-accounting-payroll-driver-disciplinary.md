# ADR-048: Sage Pastel Accounting & Payroll Integration, Driver Disciplinary Notices, and BCEA-Compliant Wage Deductions

## Status

Accepted.

## Context

Following commercial feedback and client demonstrations for South African bulk transport operators, three interconnected enterprise capabilities were requested:
1. **Sage Pastel Accounting Integration**: Bulk transport operators in South Africa predominantly use Sage Pastel (Partner and Evolution) as their general ledger and debtor accounting system. Invoices generated in LoadIx from customer contracts, reconciliation, and weight-based rate calculations need to be exported into Pastel-compatible batch format for debtor subledger import.
2. **Progressive Driver Warnings & Disciplinary Management**: Bulk haulage operations require strict adherence to road transport safety, cargo integrity, speed limits, and route compliance. Operators require progressive disciplinary tracking adhering to the South African Labour Relations Act (LRA) Code of Good Practice (Schedule 8).
3. **BCEA-Compliant Driver Deductions & Sage Pastel Payroll Integration**: When vehicle accidents, equipment damage, unauthorized diesel consumption, or traffic fines occur, operators recover these costs via payroll deductions. Under South African law (Basic Conditions of Employment Act No. 75 of 1997, Section 34), employers cannot unilaterally deduct monies from employee remuneration: deductions require a signed written acknowledgement of debt/agreement specifying the reason and calculation, and aggregate deductions for damage/loss must not exceed 25% of the employee's remuneration. These scheduled installments must feed seamlessly into Sage Pastel Payroll.

## Decision

### 1. Sage Pastel Accounting Integration
- Customer invoice batches are exported in standard Sage Pastel Partner/Evolution multi-line batch CSV format:
  - Header lines and transaction rows capture customer account codes, document numbers, invoice dates, financial period numbers, tax type indicators, net amounts, VAT amounts, and descriptions.
  - Endpoint `GET /api/v1/finance/invoice-batches/{id}/export-pastel.csv` streams generated Pastel CSV files with RFC 9457 error handling and tenant isolation.
  - Formatter logic is encapsulated in `PastelInvoiceBatchFormatter` inside `LoadIx.Infrastructure.Finance`.

### 2. Progressive Driver Warnings & Disciplinary System
- Aggregate root `DriverWarning`:
  - Categorized into progressive sanction levels: `Verbal`, `FirstWritten`, `SecondWritten`, `FinalWritten`, and `DismissalNotice`.
  - Infraction categories: `SpeedingAndSafety`, `UnauthorizedRouteDeviation`, `CargoTamperingOrContamination`, `DocumentationAndPodNeglect`, `GrossMisconduct`, and `WeighbridgeOverload`.
  - Lifecycle: `Draft -> Active -> Expired / Cancelled / Appealed`.
  - Audited with incident dates, issuance dates, issuing manager identities, expiration timeframes, driver acknowledgment notes, and evidence document attachments.

### 3. BCEA §34 Compliant Driver Recovery Deductions
- Aggregate root `DriverDeduction`:
  - Covers liabilities: `VehicleDamage`, `TrafficFine`, `FuelDiscrepancy`, `CashAdvance`, `WeighbridgePenalty`, and `General`.
  - Enforces BCEA Section 34 rules:
    - Written agreement reference / signed acknowledgement document identifier.
    - Total liability amount, scheduled installment per pay run, and running remaining balance.
    - Statutory 25% maximum wage threshold indicator / warning.
  - Lifecycle: `Draft -> Active (Approved) -> Completed (Fully Recovered) / Cancelled`.
  - Concurrency protected via EF Core optimistic concurrency tokens (`RowVersion`).

### 4. Sage Pastel Payroll Integration
- Aggregate roots `PayrollBatch` and `PayrollBatchLine`:
  - `Driver.EmployeeNumber` links driver master data to the payroll master.
  - Pay frequencies: `Weekly`, `Fortnightly`, and `Monthly`.
  - Automated pay-run aggregation: scans active driver deductions, determines eligible installments within the period window, and creates line items with Pastel transaction codes (`DED-DMG`, `DED-TRAF`, `DED-ADV`, `DED-DIES`, `DED-WEIGH`, `DED-GEN`).
  - Approval workflow: `Draft -> Approved -> Exported`.
  - Dedicated CSV export endpoint `GET /api/v1/finance/payroll-batches/{id}/export-pastel.csv` generates formatted batches for direct import into Sage Pastel Payroll.
  - Standard CSV export `GET /api/v1/finance/payroll-batches/{id}/export.csv` provides universal human-readable summaries.

### 5. Frontend User Experience
- **Master Data (Fleet / Drivers)**: Added employee payroll number tracking and a slide-over modal for driver warnings and recovery deduction scheduling.
- **Finance Hub**: Added dedicated "Driver Payroll & Deductions (Sage Pastel)" tab alongside "Customer Invoicing & Tax", with live deduction calculation preview, BCEA compliance badges, batch creation, approval, and 1-click Pastel export downloads.
