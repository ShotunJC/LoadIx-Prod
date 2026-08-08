# LoadIx Product Scope

> Workflow update: contract creation/editing is a five-step guided flow with
> inline master-data creation; load creation is a four-step draft/plan flow.
> Routes may require collection weighing, delivery weighing, both, or neither.
> See `docs/workflow-improvements.md`.

## Purpose

LoadIx is a business-to-business, multi-tenant logistics management platform for
bulk transport operators. It provides an auditable operational record from a
customer's transport agreement to completed, reconciled, invoice-ready trips.

The initial market is South African operators managing approximately 5 to 100
trucks in mining, agriculture, and construction-material transport. The design
must remain suitable for later use outside South Africa.

## Product Outcomes

The MVP should help an operator:

- translate customer contracts and load instructions into dispatchable trips;
- assign eligible drivers, vehicles, and trailers;
- capture source and destination weights with supporting evidence;
- show controllers the current state and exceptions across active trips;
- reconcile weight differences consistently and visibly;
- collect proof of delivery in a low-connectivity driver workflow;
- calculate billable revenue, transporter cost, and estimated trip profitability;
- prepare approved trips for invoice generation in the operator's finance system;
- provide customers with appropriately restricted shipment visibility; and
- maintain a traceable audit history of operational and financial decisions.

## Personas

| Persona | Primary needs |
| --- | --- |
| Tenant owner/administrator | Configure the company, branches, users, policies, and defaults |
| Operations manager | Monitor throughput, exceptions, service performance, and workload |
| Controller | Create trips, assign resources, update workflow state, and resolve exceptions |
| Fleet manager | Maintain driver, vehicle, and trailer availability and compliance data |
| Finance user | Approve reconciliation, review charges/costs, and prepare invoice batches |
| Driver | Receive work, record milestones and weights, capture documents, and sync reliably |
| Customer viewer | View only the customer's permitted instructions, trips, and documents |
| Read-only auditor | Inspect records and immutable event history without changing data |
| Platform administrator/support | Operate the SaaS platform without acquiring implicit access to tenant data |

## MVP Business Flow

1. An authorized tenant user creates a customer and transport contract.
2. A load instruction records the contracted movement request and planned
   quantity.
3. One or more trips are planned against the instruction.
4. A controller assigns a driver, vehicle, and optional trailer set.
5. The driver or controller records collection arrival.
6. Source weighbridge measurements and evidence are captured.
7. The trip departs and may publish trusted location updates.
8. Delivery arrival and destination measurements are captured.
9. Source and destination net weights are reconciled against configured tolerance.
10. Shortage, excess, and exception outcomes are reviewed.
11. Proof of delivery is captured and the trip is completed.
12. Revenue, transporter cost, and estimated profitability are calculated from
    versioned rate snapshots.
13. Authorized finance users approve eligible trips into an invoice-preparation
    batch.

Workflow transition rules, correction rules, and approval segregation must be
explicit application policies. They must not rely only on disabled buttons in the
web application.

## MVP Capability Boundaries

### Platform and tenancy

- Tenant provisioning and lifecycle
- Company profile, branches, time zone, currency, and operational defaults
- User invitations, membership, roles, granular permissions, and account security
- Platform support access that is explicit, time-bound, justified, and audited

### Master data

- Customers and their authorized customer users
- Collection and delivery locations, including optional geofences
- Drivers, vehicles, trailers, commodities, and availability/status
- Transport contracts, effective dates, rate definitions, and tolerance rules

### Planning and execution

- Load instructions and planned quantities
- Trip creation and resource assignment
- Controlled trip state transitions
- Source/destination weights and supporting documents
- Driver PWA with offline-capable mutation queue and visible sync status
- Location updates and a live operations board
- Operational notifications and exception queues

### Reconciliation and finance preparation

- Source/destination net calculations
- Shortage/excess calculation and configurable tolerance outcome
- Manual review with reason and audit trail
- Immutable pricing/cost snapshots for completed trips
- Estimated trip profitability
- Approval and grouping into invoice-preparation batches
- Export/integration-ready batch details

### Assurance

- Immutable audit events for sensitive and financial actions
- Tenant-scoped authorization
- Health and observability endpoints
- Retention-aware document storage and authorized retrieval

## Explicitly Out of Scope for MVP

Unless promoted by a later decision, the MVP does not include:

- a general accounting ledger, debtor management, payment allocation, or tax
  filing;
- direct submission of tax invoices to SARS;
- automated payroll, driver wages, fuel-card settlement, or workshop management;
- route optimization, marketplace/load bidding, or carrier tendering;
- full telematics hardware management or guaranteed real-time tracking;
- automated mine ERP or weighbridge hardware integration beyond an explicit
  import/API contract;
- native iOS or Android applications;
- public anonymous shipment tracking;
- advanced machine-learning predictions;
- multi-currency settlement within one contract; or
- microservices and independent module deployments.

Invoice preparation means producing an approved, auditable batch and export/API
payload. Issuing a legally compliant tax invoice remains an accounting-system
responsibility until requirements say otherwise.

## Product Policies Requiring Discovery

These questions affect domain rules and must be resolved before their milestone:

- Can a load instruction span multiple collection or delivery locations?
- Can vehicles change after a trip has departed, and who may approve that change?
- Which weight is billable: source, destination, lower of the two, or a
  contract-specific rule?
- Are tolerances absolute, percentage-based, commodity-specific, or combined?
- How are rejected loads, diversions, split deliveries, returns, and cancelled
  trips represented?
- Are subcontracted transporters in the first operational release?
- Which documents are mandatory by commodity, customer, or location?
- What constitutes finance approval, and is four-eyes approval required?
- Which accounting packages and export formats have priority?
- What location update sources are trusted, and what consent/retention rules
  apply?
- What are the retention periods and deletion/legal-hold policies by data class?

Until answered, implementation must use configurable policies where practical and
must not encode assumptions as irreversible data shapes.

## Non-Functional Scope

### Security and privacy

- Tenant data is deny-by-default and inaccessible across tenant boundaries.
- Authorization is permission/policy based and enforced server-side.
- Authentication uses ASP.NET Core Identity, short-lived JWT access tokens, and
  hashed rotating refresh tokens with reuse detection and revocation.
- Sensitive support access and financial changes create immutable audit events.
- Uploads are quarantined until validation/malware scanning policy completes.
- POPIA-aligned data minimization, purpose limitation, access, retention, and
  audit controls are design requirements. Technical controls alone do not amount
  to a claim of legal compliance.

### Localization and data correctness

- ZAR is the default currency, but currency remains explicit in financial data.
- Africa/Johannesburg is the default tenant time zone.
- UTC instants are stored; local time and offset are captured when operationally
  relevant and displayed using tenant/user settings.
- Money and physical quantities use fixed-precision decimal types with documented
  units and rounding.
- Phone and registration validation supports South African formats without
  preventing future international formats.

### Usability and resilience

- The desktop application favors operational density, quick filtering, keyboard
  use, and prominent exception states.
- The driver application uses large touch targets, minimal typing, camera capture,
  low-bandwidth payloads, and explicit offline/sync feedback.
- Mobile retries must not duplicate operational events.
- Core operational reads degrade predictably when optional infrastructure such as
  live updates or notifications is unavailable.

## MVP Success Measures

Initial targets require validation during discovery. Suggested measures are:

- at least 95% of completed trips have required weight evidence and proof of
  delivery attached;
- controllers can identify overdue and exception trips from one operations view;
- no unresolved cross-tenant access finding in automated security tests;
- duplicate mobile submissions produce one business outcome;
- reconciliation calculations are reproducible from stored inputs and policy
  versions;
- every invoice-batch inclusion can be traced to actor, time, trip snapshot, and
  approval; and
- pilot users can complete the primary driver workflow under simulated
  intermittent connectivity.

## Assumptions

- One deployment hosts many tenant companies.
- A user can eventually belong to more than one tenant, but operates within one
  explicitly selected tenant context per request/session.
- A trip belongs to exactly one tenant, contract, load instruction, customer, and
  commodity; stops and resource assignments may evolve through later
  requirements.
- Object storage holds binary documents; SQL Server holds metadata and audit
  linkage.
- External invoicing and telematics integrations use versioned APIs/events rather
  than direct database access.
- English is the initial UI language; localization readiness is retained.
