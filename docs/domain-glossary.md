# LoadIx Domain Glossary

This glossary establishes a shared language. Definitions marked **provisional**
must be confirmed through domain discovery before their model is finalized.

## Core Parties and Organization

**Platform**  
The LoadIx SaaS environment serving multiple tenants.

**Tenant**  
The security and data-isolation boundary for a subscribing transport business.
Tenant identity is derived from authenticated membership, never trusted from a
business request body.

**Tenant ID**  
A strongly typed identifier carried by every tenant-owned record. Tenant IDs in
request payloads do not establish authority; a trusted request or background-job
context supplies the effective tenant.

**Company**  
The legal or trading organization represented by a tenant. The MVP assumes one
primary company per tenant; group-company support is provisional.

**Branch**  
An operational subdivision of a tenant, such as a regional depot. Branch scope
does not replace tenant scope.

**User**  
A platform identity able to authenticate. A user may hold one or more tenant
memberships.

**Tenant membership**  
The association between a user and tenant, including status, assigned roles, and
permitted customer/branch scope.

**Invitation**  
A single-use, expiring onboarding grant created by an authorized tenant
administrator. Possession of the emailed invitation proves access to the invited
mailbox; there is no public self-registration.

**Session**  
A refresh-token-backed authenticated browser/device relationship. Sessions can
be listed and revoked independently. Access tokens are short-lived projections
of the current user, tenant, roles, and permissions.

**Refresh-token family**  
The chain created as a refresh token rotates. Reuse of any revoked predecessor is
treated as probable theft and revokes every active token in that family.

**Customer**  
An organization for whom the tenant transports commodities. Legal and trading
names, registration/tax references, payment terms, billing address, and active
status belong to the customer record.

**Contact person**  
A fictional or real operational/finance contact associated with a customer.
Contact details are tenant-owned and kept to the minimum required for operations.

**User profile**  
Tenant-specific business profile data associated with a future platform
identity. It is not an authentication credential or membership model.

**Customer user**  
A restricted tenant membership linked to one or more customers and allowed to
view only authorized shipment data.

**Transporter / subcontractor**  
A third party performing transport on behalf of the tenant. **Provisional and
not guaranteed in the MVP.**

## Master Data

**Location**  
A named operational site with address and optional coordinates, such as a mine,
silo, quarry, depot, weighbridge, warehouse, port, or delivery site.

**Geofence**  
A geometric boundary around a location used to interpret position observations.
A geofence event is evidence, not necessarily proof of a workflow transition.

**Commodity**  
The bulk material being transported, with a name, code, and relevant measurement
unit/classification.

**Driver**  
A person authorized and eligible to operate assigned vehicles. A driver may be
linked to a user account for the PWA.

**Vehicle**  
The powered hauling unit, usually a truck/tractor, identified by a fleet number
and registration.

**Trailer**  
A non-powered hauling unit assigned to a trip. **Trailer set** means one or more
trailers treated as an assignment.

**Resource eligibility**  
Whether a driver, vehicle, or trailer may be assigned at a point in time based on
status, availability, and later compliance rules.

## Commercial Planning

**Transport contract**  
The agreement with a customer governing a movement type during an effective
period, including locations, commodity, rates, tolerances, and commercial rules.

**Rate definition**  
A typed, effective-dated rule describing how transport revenue is calculated.
The initial methods are fixed per trip, source/destination/reconciled net tonne,
and base plus reconciled-tonne. A rate may add a fuel percentage and minimum
charge; it is not a user-programmable expression.

**Rate snapshot**  
An immutable copy of the selected rate, calculation inputs, rounding rule, and
breakdown retained with a financial outcome so later contract edits do not alter
historical trip values.

**Contract route**  
A source/destination location pair within a contract, identified by a
human-readable route reference. Each route requires at least one non-overlapping
effective rate before activation.

**Tolerance rule**  
A versioned rule defining an acceptable difference between measurements. The
initial contract value is a percentage from 0 through 100; combined absolute and
commodity-specific tolerances remain a later decision.

**Load instruction**  
A customer's authorized request to move a planned quantity under a contract. It
is fulfilled by one or more trips.

**Planned quantity**  
The requested or committed commodity amount on a load instruction, with explicit
unit.

**Trip**  
One execution instance moving a commodity from collection to delivery under a
load instruction. It is the primary operational workflow aggregate.

**Assignment**  
The time-bound allocation of a driver, vehicle, and trailer set to a trip.
Assignment history is retained when resources change.

**Trip milestone**  
An observed or confirmed event in the trip lifecycle, such as arrival,
weighing, departure, or delivery.

**Trip state**  
The authoritative stage in the explicit trip workflow: Planned, Assigned,
Dispatched, AtCollection, Loading, CollectionComplete, InTransit, AtDelivery,
Unloading, DeliveryComplete, ReconciliationRequired, Completed, Cancelled, or
Rejected. State is changed only by named domain commands and is never inferred
solely from timestamps.

**Trip transition**  
An append-only record of a state-machine decision containing actor, UTC time,
previous and new state, optional reason, and whether explicit manual-override
authority was used.

**Manual trip override**  
A reasoned exceptional transition or post-dispatch reassignment available only
to `Trip.Override`. It remains visible in the trip timeline and audit history.

**Trip evidence checkpoint**  
Structured source/destination gross, tare, calculated net, and weighbridge
reference information plus reconciliation/document readiness retained by the
trip until dedicated module aggregates replace these checkpoints.

**Position observation**  
A timestamped location reading with source and accuracy metadata. It may be
driver-device, telematics, or manual data and has an explicit trust level.

**Operational event**  
A tenant-scoped integration fact emitted after a trip mutation commits, such as
trip creation, assignment change, status change, weighbridge capture, document
upload, reconciliation exception, completion, delay, or resource conflict.
Clients treat the event as a cache update signal and refetch authoritative data.

**Notification**  
A tenant-owned in-app message and related delivery attempts for an operational or
account event. Notification requests have registered types, deduplication keys,
preferences, templates, retry/failure state, and provider correlation IDs.

**Notification template**  
A tenant-level subject/body template for a specific notification type and
channel. The initial renderer supports fixed token replacement, not executable
formulas.

**Notification preference**  
A user-level channel choice for a notification type. Preferences can disable
email or SMS where policy allows; in-app notification remains the default
operational channel.

## Weighing and Reconciliation

**Weighbridge measurement**  
A recorded gross or tare mass at a named source/destination, including time,
unit, capture source, and supporting evidence.

**Gross weight**  
The mass of the loaded vehicle/trailer configuration.

**Tare weight**  
The mass of the empty vehicle/trailer configuration.

**Net weight**  
The calculated commodity mass: gross weight minus tare weight for a defined
measurement set. It must retain inputs, unit, precision, and calculation version.

**Source net weight**  
The net weight determined at collection.

**Destination net weight**  
The net weight determined at delivery.

**Weight difference**  
The signed difference between source and destination net weights under the
configured convention.

**Shortage**  
A destination quantity below the source/reference quantity beyond or within
contract rules. Financial treatment is contract-specific.

**Excess**  
A destination quantity above the source/reference quantity. Financial treatment
is contract-specific.

**Reconciliation**  
The reproducible comparison of source and destination quantities using a
versioned tolerance policy, resulting in an accepted, exception, or review state.

**Tolerance exception**  
A reconciliation outcome outside the allowed tolerance and requiring an explicit
resolution or approval.

**Reconciliation override**  
An authorized decision to accept or adjust an exception, always accompanied by
reason, actor, timestamp, previous state, and audit event.

## Evidence and Finance

**Document**  
Tenant-owned metadata for an authorized binary object, including category,
checksum, media type, size, storage state, aggregate owner, uploader, UTC upload
time, optional notes, and optimistic-concurrency version. SQL holds this metadata;
an object provider holds the content under a server-generated opaque key.

**Document owner**  
The tenant-owned aggregate to which evidence is attached: initially a trip, load
instruction, driver, vehicle, or trailer. Ownership determines both valid
document categories and resource-level authorization.

**Document quarantine**  
A non-public object-storage prefix used after format validation and before
malware approval. A quarantined object is never downloadable through the
application and is deleted after rejection or successful promotion.

**Document preview**  
A derived JPEG used only for safe viewing of an uploaded image. It is
auto-oriented, dimension constrained, and stripped of metadata. It never replaces
or mutates the evidentiary original.

**Document deletion tombstone**  
The retained metadata state recording who requested deletion and when. Deletion
is permitted only before the owning trip is completed; completed-trip documents
are immutable.

**Weighbridge ticket**  
Documentary evidence supporting a weighbridge measurement.

**Proof of delivery (POD)**  
Evidence that delivery was received, commonly a signed or stamped document or
image. Required fields and signatures are customer/contract-specific.

**Billable quantity**  
The quantity selected by the contract's versioned billing rule. It is not assumed
to always equal source or destination net weight.

**Charge snapshot**  
The immutable inputs, rate version, rounding, currency, and result used to
calculate trip revenue.

**Cost snapshot**  
The immutable inputs and result used to estimate a trip's attributable transport
cost.

**Estimated profitability**  
Charge snapshot minus included cost snapshots. It is an operational estimate, not
an accounting profit claim.

**Invoice-ready trip**  
A completed trip that satisfies required reconciliation, evidence, pricing, and
approval policy and is not already allocated to an active invoice batch.

**Invoice-preparation batch**  
An approved grouping of invoice-ready trips and immutable commercial snapshots
for export or handoff to an accounting system.

**Trip financials**  
A tenant-owned financial preparation record for one completed trip. It preserves
calculation inputs, the immutable contract-rate snapshot, billable tonnes,
subtotal, surcharge, tax-exclusive amount, explicit tax treatment, tax amount,
total, calculation timestamp, calculation version, costs, approval state, and
invoice-batch allocation.

**Tenant tax configuration**  
A reviewed tenant-level default tax treatment and percentage used when preparing
trip financials. It is explicit business configuration, not a hidden system
assumption; finance users remain responsible for reviewing tax treatment.

**Cost entry**  
An auditable trip financial cost row such as driver cost, fuel, toll,
subcontractor, weighbridge, loading, unloading, or approved miscellaneous cost.
Corrections after approval or invoicing are represented as adjustment entries,
not silent rewrites.

**Financial approval**  
An explicit finance-user approval event for a trip financial record or invoice
batch. It records actor, tenant, target, outcome, UTC time, and optional reason.

**Invoice-ready trip**  
A completed, reconciled, financially approved trip that is not already assigned
to an invoice batch.

**Invoice**  
A formal accounting/tax document. Issuance is outside the initial MVP unless
requirements explicitly promote it.

## Platform and Assurance Terms

**Role**  
A named bundle of permissions, such as Controller or FinanceUser.

**Permission**  
A granular server-enforced capability, optionally further constrained by tenant,
branch, customer, resource ownership, and workflow state.

**Support context**  
An explicit, audited tenant context entered only by a platform administrator. It
is visible in response headers and structured logs and never grants an ordinary
tenant user cross-tenant access.

**Audit event**  
An append-only business/security record describing who did what, when, in which
tenant, to which entity, and with what outcome/reason. It is distinct from an
operational log.

**Idempotency key**  
A client-generated unique operation identifier that lets the server return the
same outcome for a safely retried command rather than repeat the business action.

**Load instruction**  
A tenant-owned customer request for a positive quantity of one permitted
commodity over one active contract route within explicit collection and delivery
windows. Its lifecycle is Draft, Planned, InProgress, Completed, or Cancelled.

**Generated trip reservation**  
The stable trip identity and planned quantity created from a planned load
instruction. It is an executable planning boundary, not yet the later dispatch
aggregate with resource assignment and driver workflow.

**Instruction fulfilment**  
Completed quantity is the sum of completed generated trips in the instruction's
unit. Remaining quantity is requested minus completed quantity and may be
negative to make over-delivery explicit. Under- and over-delivery are always
reported separately.

**Optimistic concurrency**  
A control that rejects an update when the record changed after the caller read
it, preventing silent overwrite.

**Reference code**  
A tenant-scoped, human-friendly business identifier such as a branch code, site
code, registration, licence number, or fleet number. GUIDs remain API identities
but are not the primary operator-facing label.

**Operational availability**  
Whether an active vehicle or trailer is presently selectable for work. It is
independent from active/inactive master-data status and does not yet imply a trip
assignment or maintenance workflow.

**Compliance status**  
A derived `Valid`, `Expiring`, or `Expired` indicator based on the earliest
applicable licence or roadworthy expiry. The initial warning window is 30 days.

**Domain event**  
A fact raised by domain behavior and handled within the application boundary.

**Result**  
An explicit success-or-domain-error return value used by factories and behavior
that can reject a business state. Expected validation failures are not
exceptions.

**Money**  
A decimal amount paired with a three-letter ISO-style currency code. The domain
supports four decimal places and persistence uses `decimal(19,4)` when a money
value is stored.

**Weight**  
A non-negative decimal quantity paired with an explicit kilograms or tonnes
unit. Persistence uses three decimal places; unit conversion is explicit.

**Audit metadata**  
UTC creation/modification instants and optional actor identifiers attached to
mutable entities. Current time comes from `TimeProvider`, not direct system-clock
calls.

**Integration event**  
A versioned, durable message published through the outbox for asynchronous or
external consumers.

**Outbox**  
A database table written in the same transaction as business data so integration
events can be published reliably after commit.

**Sync state**  
The driver's visible status for local work: pending, syncing, confirmed, or
failed/conflicted.

**Driver location update**  
An explicit, permission-gated coordinate capture for a trip assigned to the
authenticated driver. It records capture and receipt time plus optional accuracy;
it is not covert or background tracking.

**Operational report**
A tenant-scoped, bounded query over operational and financial records. Reports
are read-only decision support and carry a generated-at timestamp and the
tenant's reporting time zone.

**Invoice-ready revenue**
The tax-exclusive amount calculated from a trip's immutable rate snapshot and
reconciled billable tonnes. It is preparation data, not a posted accounting
invoice.

## Units and Language Rules

- Always qualify ambiguous quantities with a unit.
- Use `mass` for the physical quantity; retain familiar UI wording such as
  "weight" where operators expect it.
- Money always includes currency; ZAR is only the default.
- `Completed`, `approved`, `reconciled`, `invoice-ready`, and `invoiced` are
  distinct states.
- A position update must not be called proof of arrival unless the workflow policy
  has accepted it as such.
- A deleted UI item is not necessarily physically erased; use precise lifecycle,
  retention, and erasure terms.
- Initial master data uses explicit active/inactive status. Soft deletion is not
  applied without a documented retention or recovery requirement.
