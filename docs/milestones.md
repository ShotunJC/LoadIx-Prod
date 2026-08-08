# LoadIx Delivery Milestones

## UX and workflow improvement milestone

Implemented guided contract and load workflows, inline contract master-data
creation, active contract editing with historical protections, a two-trailer
maximum, optional route weighbridges, quantity-source reporting, grouped
permission-aware navigation, migration, and regression coverage. Detailed user
guidance is in `docs/workflow-improvements.md`.

## Delivery Principles

- Deliver vertical, testable capabilities while establishing shared foundations
  only when the next slice needs them.
- Tenant isolation, authorization, audit, concurrency, idempotency, and
  observability are built into each milestone.
- Each milestone must meet the repository definition of done before dependent
  work begins.
- Validate domain policies with representative operators before making
  irreversible model decisions.

## Execution Order

```mermaid
flowchart TD
    M0["M0 Scope and architecture baseline"] --> M1["M1 Engineering foundation"]
    M1 --> M2["M2 Identity and tenancy"]
    M2 --> M3["M3 Operational master data"]
    M3 --> M4["M4 Commercial setup and load instructions"]
    M4 --> M5["M5 Trip planning and dispatch"]
    M5 --> M6["M6 Driver execution, documents, and weighing"]
    M6 --> M7["M7 Reconciliation and completion"]
    M5 --> M8["M8 Live operations and notifications"]
    M7 --> M9["M9 Finance preparation and profitability"]
    M8 --> M10["M10 Customer visibility"]
    M9 --> M10
    M10 --> M11["M11 Production hardening and pilot"]
    M6 --> M15["M15 Edge-device and weighbridge integration foundation"]
    M15 --> M11
```

M8 can proceed alongside late M6/M7 work once trip events are stable. Finance must
not start before reconciliation and immutable pricing inputs are defined.

## M0 — Scope and Architecture Baseline

**Outcome:** A shared product vocabulary, architecture direction, engineering
rules, and delivery sequence.

**Deliverables:**

- repository assessment;
- `AGENTS.md` and the initial documentation set;
- recorded decisions, assumptions, open discovery questions, and proposed
  structure; and
- agreed MVP boundary.

**Exit criteria:** Stakeholders confirm the MVP interpretation and identify owners
for unresolved domain, security, finance, and operations decisions.

## M1 — Engineering Foundation

**Outcome:** A runnable, observable, tested skeleton for local development and CI.

**Status:** Implemented and verified on 2026-07-28.

**Deliverables:**

- .NET solution/projects and React/Vite application;
- pinned SDK/runtime and centralized package management;
- formatting, analyzers, nullable references, and warning policy;
- Compose services for SQL Server, Redis, RabbitMQ, MinIO, and required local
  observability;
- configuration validation, Problem Details, OpenAPI, health checks, Serilog, and
  OpenTelemetry baseline;
- EF Core context/migration and Testcontainers test harness;
- architecture-test rules;
- frontend design tokens, application shell, API client generation, and test
  harnesses;
- Dockerfiles, GitHub Actions, `.env.example`, and quick-start README.

**Key tests:** solution build, container health, database migration, API smoke,
architecture boundaries, frontend render/build, and CI reproducibility.

**Decision gates:** MediatR versus native handlers; TanStack Router versus React
Router; telemetry backend; package set.

## M2 — Identity, Tenancy, and Authorization

**Outcome:** Users can authenticate and operate only within explicitly authorized
tenant contexts.

**Status:** Authentication, invitation-based user administration, tenant
authorization, session security, and audited support context implemented and
verified on 2026-07-28. Authentication screens were delivered as the next
requested product milestone even though this roadmap groups the capability under
M2.

**Deliverables:**

- tenant, user, membership, role, and permission model;
- ASP.NET Core Identity flows, invitation/reset policy, JWT access tokens, rotating
  hashed refresh tokens, reuse detection, and revocation;
- trusted tenant-context resolution and tenant-aware persistence controls;
- platform roles and controlled support-access workflow;
- permission policies, audit events, auth rate limits, and secure browser session
  design;
- company/branch settings including currency and time zone.

**Key tests:** two-tenant read/write isolation, guessed IDs, membership switching,
permission denial, refresh rotation/reuse, revoked membership, support access,
and audit immutability.

**Decision gates:** MFA requirements, federation, membership multiplicity,
support/break-glass policy, session storage threat model.

## M3 — Operational Master Data

**Outcome:** Authorized users can maintain the resources needed to plan work.

**Status:** Implemented and verified on 2026-07-28 as the requested Milestone 4
master-data slice. The request numbering is one ahead of this original roadmap
because authentication was delivered separately.

**Deliverables:**

- customers and customer access scope;
- locations and geofences;
- drivers, vehicles, trailers, commodities, status, and availability;
- bounded/filterable/stably sorted lists;
- concurrency controls, indexes, validation, audit events, and synthetic seed data;
- responsive management UI with loading, empty, error, and conflict states.

**Key tests:** tenant/customer scoping, uniqueness within tenant, concurrency
conflicts, validation/local formats, pagination bounds, and stable sorting.

**Decision gates:** compliance-expiry scope, trailer combinations, geofence
shapes, subcontractor inclusion.

## M4 — Contracts and Load Instructions

**Outcome:** Commercial terms can produce controlled requests for transport.

**Status:** Complete. The transport-contract and rate-calculation slice was
implemented on 2026-07-28 as requested Milestone 5. The load-instruction,
quantity-progress, lifecycle, and idempotent trip-generation slice was implemented
as requested Milestone 6.

**Deliverables:**

- effective-dated contracts, lanes, commodities, rates, tolerance definitions,
  and document requirements;
- load-instruction lifecycle and planned quantity;
- immutable/versioned commercial rule handling;
- create/review/list UI and audit history.

**Key tests:** effective-date selection, decimal precision/rounding, invalid
overlaps, authorization, concurrency, instruction quantity rules, and historical
version retention.

**Decision gates:** billable quantity methods, rate types, tolerance algorithms,
multi-stop/split instructions, cancellation/amendment policy.

## M5 — Trip Planning and Dispatch

**Outcome:** Controllers can split instructions into trips, assign resources, and
dispatch eligible work.

**Status:** Implemented as requested Milestone 7. Generated reservations are
promoted to tenant-owned operational trips with immutable rate snapshots,
explicit lifecycle commands, assignment/conflict checks, command idempotency,
concurrency, audit history, and a dispatch workspace.

**Deliverables:**

- trip aggregate and explicit state machine;
- resource assignment/history and eligibility checks;
- instruction fulfillment totals;
- controller trip board/list with dense filtering and keyboard-friendly actions;
- idempotent trip creation and dispatch commands;
- concurrency, audit, and domain events.

**Key tests:** legal/illegal transitions, duplicate commands, conflicting
assignments, concurrent edits, quantity constraints, tenant boundaries, and
permission/state checks.

**Decision gates:** post-dispatch reassignment, rejected/diverted/returned flows,
resource scheduling conflicts.

## M6 — Driver Execution, Documents, and Weighing

**Outcome:** Drivers can execute assigned trips and capture reliable evidence in
intermittent connectivity.

**Current implementation note:** The secure document and proof-of-delivery slice
requested as Milestone 9 is implemented: tenant-scoped metadata, MinIO-compatible
storage, quarantine and validation, fail-closed malware scanning abstraction,
authorized streaming, safe image previews, trip UI, idempotency, audit, and
completed-trip immutability. Offline document queues, driver PWA execution, and
dedicated weighing remain part of the broader M6 work.

**Driver mobile implementation note:** The requested Milestone 10 driver PWA
adds an installable web manifest/service worker, assignment-scoped driver
workspace, mobile controls, IndexedDB command queue, idempotent retry, explicit
sync/conflict state, camera-friendly evidence capture, and permission-gated
location updates. Browser background sync and background tracking remain future
integration decisions rather than simulated capabilities.

**Deliverables:**

- installable responsive driver PWA and minimal assigned-work sync;
- local mutation/document queue with explicit sync and conflict state;
- arrival/departure milestones and optional location observations;
- source/destination gross and tare measurements with net calculation;
- two-phase document upload, quarantine/validation/scanning, POD and weighbridge
  categories, authorized download;
- idempotency, concurrency, audit, and telemetry.

**Key tests:** offline/reconnect, duplicate submission, out-of-order operations,
upload signature/size/type failures, unauthorized document access, measurement
precision, device revocation, and cross-tenant object-key isolation.

**Decision gates:** malware provider, object retention, trusted GPS sources,
offline data lifetime, mandatory evidence rules.

## M7 — Reconciliation and Trip Completion

**Outcome:** Completed movements have reproducible quantity outcomes and controlled
exception resolution.

**Deliverables:**

- source/destination comparison and versioned tolerance evaluation;
- shortage/excess and exception queue;
- review/override workflow with reasons and approvals;
- trip completion eligibility and immutable completion snapshot;
- operational reconciliation UI and audit trail.

**Key tests:** calculation boundaries, units/rounding, tolerance edge cases,
missing evidence, unauthorized override, approval segregation, concurrency, and
historical reproducibility.

**Decision gates:** shortage liability and adjustment rules, four-eyes approval,
correction/reopen policy.

## M8 — Live Operations and Notifications

**Outcome:** Controllers see current operational state and receive actionable
exception signals.

**Deliverables:**

- purpose-built operations read model;
- SignalR tenant-scoped updates with reconnect/refetch behavior;
- status, aging, exception, branch/customer, and assignment filters;
- map view with provider-safe tile usage;
- notification requests/preferences, Hangfire scheduling/retry, and delivery
  outcomes;
- outbox publication and idempotent consumers.

**Key tests:** hub authorization/group isolation, stale reconnect, read-model
eventual consistency, broker/provider outage, notification deduplication, and
time-zone scheduling.

**Decision gates:** channels/providers, escalation rules, SLA thresholds, map
provider/licensing.

## M9 — Profitability and Invoice Preparation

**Outcome:** Finance users can understand estimated trip economics and prepare
approved batches for accounting.

**Deliverables:**

- immutable charge and cost calculations/snapshots;
- profitability views with transparent inputs;
- invoice-ready eligibility and approval;
- invoice-preparation batches, locking, removal/cancellation rules, and
  export/versioned integration payload;
- strong audit and concurrency controls.

**Key tests:** decimal precision, currency and rounding, rule-version
reproducibility, duplicate batch prevention, conflicting approvals, unauthorized
financial access, and export totals.

**Decision gates:** cost components, VAT/accounting treatment, accounting target,
approval segregation, correction/credit workflow. Obtain finance/legal review;
do not represent technical behavior as tax compliance.

## M10 — Customer Visibility

**Outcome:** Customer users see only their authorized shipment information and
documents.

**Deliverables:**

- customer-scoped membership/access model;
- shipment status, milestones, reconciliation-approved quantities, and permitted
  POD views;
- restricted notifications or exports if approved;
- access auditing, rate limits, and privacy-aware presentation.

**Key tests:** customer-to-customer isolation inside one tenant, tenant isolation,
document-category authorization, revoked access, enumeration resistance, and
safe error responses.

**Decision gates:** visible fields/statuses, delay/embargo rules, branding,
anonymous tracking (currently out of scope).

## M11 — Production Hardening and Pilot

**Outcome:** The system is deployable, recoverable, supportable, and validated
with pilot operators.

**Deliverables:**

- threat-model closure and independent security review;
- performance/capacity tests and query/index tuning;
- accessibility and representative-device/low-bandwidth testing;
- backup/restore and disaster-recovery exercise;
- SLOs, dashboards, alerts, runbooks, incident and support-access procedures;
- retention/erasure/legal-hold workflows;
- dependency/container scans, SBOM, deployment automation, rollback strategy;
- pilot onboarding, training, feedback loop, and go-live checklist.

**Key tests:** load/soak, failure recovery, restore verification, tenant isolation
regression, authorization matrix, offline endurance, browser/device compatibility,
and operational drills.

**Decision gates:** hosting topology, RPO/RTO, availability targets, regional data
requirements, support model, legal/privacy sign-off.

## M15 — Edge-device and Weighbridge Integration Foundation

**Outcome:** A secure LoadIx Edge Agent boundary can submit structured
weighbridge observations and remain recoverable across intermittent
connectivity, without putting hardware-specific serial parsing in the main API.

**Status:** Implemented on 2026-08-01. Domain/application/API, corrective SQL
migrations, simulator, administration UI, parser tests, and security-focused
integration coverage are present. SQL-backed integration execution remains
dependent on the repository's Docker/Testcontainers environment being available.

**Deliverables:**

- tenant-bound `EdgeDevice`, credential, weighbridge, raw/stable reading,
  command, heartbeat, and ticket-submission records;
- client-generated recoverable bootstrap, device-specific hashed credentials,
  overlap-and-confirm rotation, revocation, timestamp tolerance,
  nonce/idempotency replay protection, device-partitioned rate limits, failed
  audit evidence, restrictive relational constraints, and RFC Problem Details;
- authenticated endpoints for bootstrap, rotation, heartbeat, configuration,
  weight readings, ticket submissions, and diagnostics;
- application-level parser-plugin contract, generic line parser, and a .NET
  simulator with atomic local state, recoverable bootstrap/rotation, local
  pending queue, retries, disconnect simulation, heartbeat, and confirmation;
- tenant administrator UI for device inventory, online/offline state, heartbeat,
  site assignment, credential actions, diagnostics, and live simulated readings;
- ADR-045 documenting the Raspberry Pi/industrial-PC deployment boundary and
  the requirement for verified manufacturer protocol documentation.

**Key tests:** authentication and failed security audits, client-generated
bootstrap replay/lost-response recovery, nonce/idempotency mismatch,
concurrent duplicate submissions, overlap-and-confirm rotation, revocation,
stale timestamps, cross-tenant device hiding, offline captured-time replay,
stable-reading deduplication, relational constraints, and generic parser
acceptance/rejection.

**Decision gates:** verified indicator protocol fixtures, local agent secret-store
choice, queue retention/backpressure limits, hardware electrical safety review,
and production certificate/identity management.

## M16 - Audit, Compliance Controls, and Security Hardening

**Outcome:** LoadIx has a reviewable security baseline for pilot preparation,
with immutable audit evidence, stronger session and tenant controls, defensive
HTTP/upload behavior, and explicit data-governance foundations.

**Status:** Implemented and verified on 2026-08-03. This milestone provides
technical foundations and does not claim legal compliance.

**Deliverables:**

- immutable, redacted audit events for authentication, authorization, support,
  operational, document, finance, export, and edge-device actions;
- session invalidation, refresh-token reuse detection, account lockout, tenant-
  scoped authorization, support reasons, rate limiting, security headers, CORS,
  trusted-proxy handling, and edge request throttling;
- governance foundations for retention policy, personal-data inventory, bounded
  tenant export, deactivation, anonymisation workflow, and support logging;
- dependency/container scan workflow, refreshed API runtime image, deployment
  safeguards, security regression tests, ADR-0016, and `docs/security-review.md`.

**Key tests:** cross-tenant access, privilege escalation, object-ID tampering,
revoked/deactivated authentication, malicious uploads, replay and duplicate
submissions, mass assignment, edge rate limits, and audit-summary redaction.

**Decision gates:** independent security/privacy review, managed secrets and
malware scanner, distributed rate limiting/WAF, retention and legal-hold
procedure, anonymisation scope, backup/object-store lifecycle, and pilot
acceptance of remaining scan findings.

## M17 — Performance, Reliability, and Production Readiness

**Outcome:** LoadIx has bounded operational reads, explicit retry/dead-letter
behavior, production-oriented telemetry, and a repeatable realistic pilot
workload without inflating normal development startup.

**Status:** Implementation complete on 2026-08-03. A generated-workload k6 run
and representative query-plan review are recorded in
`docs/performance-results.md`; failure drills and deployment-scale capacity
remain pilot decision gates.

**Deliverables:**

- SQL Server connection resiliency, transient-only provider retries with
  exponential backoff and circuit breakers for broker, object storage, and SMTP;
- scheduled operational-outbox retries with bounded attempts and dead-letter
  state, durable report-export dead-letter routing, background-job retry logging,
  graceful host shutdown, and dependency health checks;
- N+1 removal and bounded/status/date-indexed operations-board, location, trip,
  and report query paths, with EF migration artifacts;
- structured trace correlation, SQL/OpenTelemetry instrumentation, request and
  workload metrics, notification/job failure counters, queue-depth gauges,
  active-trip counts, and edge-device online counts;
- SignalR reconnect behavior and offline mobile replay classification for
  transient versus permanent failures;
- an explicit generated workload command for 10 tenants, 100 users, 1,000
  drivers, 1,000 vehicles, 10,000 load instructions, and 100,000 trips with
  related operational measurements/events;
- maintainable k6 coverage for login, trip list, operations board, status
  transition, location update, weighbridge submission, SignalR, and report
  request, plus reproducible environment/result documentation.

**Key tests:** paginated-memory/query-plan review, transient/permanent retry
classification, outbox and report dead-letter behavior, offline replay states,
SignalR reconnect, stale-version conflict handling, generated-data counts, and
the documented k6 threshold run.

**Decision gates:** production telemetry backend and alerts, multi-replica worker
claim/lease strategy, export artifact storage, and measured pilot capacity.

## M18 - Pilot Deployment Foundation

**Outcome:** LoadIx has production-oriented containers, an explicit
dedicated-server/VPS pilot topology, protected CI/CD deployment and rollback
workflows, persistent MinIO/S3 object and Data Protection storage, a separate
worker, explicit database migrations, health probes, smoke tests, and
operational pilot documentation.

**Status:** Implementation complete on 2026-08-04. No server or cloud account is
deployed by this milestone; operators must provision and approve the protected
dedicated-server environment before running the manual deployment workflow.

**Deliverables:**

- non-root, multi-stage API, web, worker, and migration images;
- Docker Compose/Caddy dedicated-server guidance with GHCR publication, private
  service networking, RabbitMQ messaging, MinIO/S3 object storage, SQL/Redis,
  and provider-portable application seams;
- production and development configuration separation, persistent host-backed
  Data Protection keys, secure secret references, TLS/reverse-proxy guidance,
  health probes, conservative autoscaling, and separate worker deployment;
- explicit EF migration job and database migration runbook;
- GitHub Actions CI, dependency/container scans, immutable image publication,
  protected pilot deployment, post-deployment smoke test, and manual rollback;
- pilot, configuration, backup/restore, rollback, incident-response, and daily
  runbook documentation, including the go-live checklist.

**Key checks:** release build/tests, frontend lint/typecheck/test/build,
`docker compose config`, Docker image builds/scans where available, migration
image review, and automated HTTPS smoke tests after a protected deployment.

**Decision gates:** approved dedicated-server hardening/firewall and access
design, protected secret/env-file process, production malware scanner,
RabbitMQ/SMTP/SMS/map providers, MinIO/S3 retention and backup design,
backup/restore rehearsal, incident contacts, privacy/DPA review, and pilot
capacity evidence.

## M19 - Realistic Pilot Demonstration Environment

**Outcome:** Sales and pilot training use a polished, deterministic and safely
resettable synthetic tenant that exercises the complete LoadIx workflow.

**Status:** Implemented on 2026-08-07.

**Deliverables:**

- a stable `LoadIx Demo Transport` tenant with representative branches,
  customers, locations, commodities, fleet, commercial terms and instructions;
- thirty-two trips distributed from planning through completion, including
  within-tolerance and approved-exception reconciliation stories;
- object-store-backed repository-generated PDF evidence, notification history,
  approved invoice-ready financials, reporting/dashboard facts and a simulated
  weighbridge edge device;
- an idempotent seed and tenant-scoped reset guarded by Development environment,
  explicit acknowledgement and production-like database-name refusal;
- twelve step-by-step sales/training scripts in `docs/demo-guide.md`; and
- demo identification, current terminology, actionable dashboard navigation,
  responsive presentation and explicit loading/error behavior.

**Key checks:** canonical entity/status counts, repeat seed and reset/reseed,
cross-tenant-safe reset scope, object download, dashboard/report facts, backend
build/tests, frontend lint/typecheck/tests/build and Compose validation.

## Cross-Cutting Work in Every Milestone

- schema migrations and realistic synthetic seed updates;
- OpenAPI and generated client consistency;
- tenant and permission tests;
- immutable audit coverage for sensitive actions;
- telemetry, health implications, and safe logging;
- accessible responsive states;
- documentation and operational notes;
- package justification and security review; and
- format, build, unit, integration, architecture, component, E2E, and Compose
  checks proportional to the change.

## Risks to Track

| Risk | Early mitigation |
| --- | --- |
| Domain variation between mining, agriculture, and construction | Validate one pilot workflow first; version policies rather than hard-code |
| Tenant data leakage | Layered tenant controls and adversarial two-tenant integration tests |
| Offline duplicate/conflicting actions | Idempotency, concurrency, explicit sync state, narrow offline data |
| Weight/rate disputes | Immutable inputs, policy versions, evidence, transparent calculation audit |
| Upload malware or data exposure | Quarantine, signature/size checks, scanning, opaque keys, authorized download |
| Infrastructure complexity too early | Keep one deployable and clear responsibilities; make optional systems non-critical |
| Finance scope expansion | Maintain invoice-preparation boundary until accounting requirements are approved |
| Public OSM tile misuse | Select a compliant map provider/cache policy before production |
| POPIA expectations mistaken for compliance | Legal/process review alongside technical privacy controls |
