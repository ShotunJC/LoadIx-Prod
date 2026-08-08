# Guided Commercial and Operations Workflows

## Contract workflow

The contract editor uses five steps: customer/details, route, commodity and
quantity rules, rates, and review. Users with the applicable master-data
permission can create customers, locations, and commodities in the workflow;
the new record is selected without clearing entered contract data.

`Save as draft` does not make the contract available for load planning. `Save
and activate` applies all activation validation, including effective dates and
the availability of the quantity source required by every rate. Active contract
edits apply to future trips. Routes and rates referenced by existing loads or
trip snapshots cannot be destructively removed or changed.

## Weight capture

Each contract route independently declares whether collection and delivery
weight capture are required. Trips snapshot both flags, the tolerance, and the
manual-quantity policy when generated. This makes all four site combinations
valid without allowing later contract edits to alter existing trips.

- Two measurements are compared using the snapshotted tolerance.
- One configured measurement becomes the reconciled quantity automatically.
- No configured measurements makes reconciliation not applicable for fixed
  billing.
- Per-tonne manual billing requires `ManualQuantityPerTonne`, contract opt-in,
  the `Trip.Override` permission, a reason, idempotency, and an audit event.
- A contract cannot activate when a measured rate lacks its required source.

Reports and trip details expose `QuantitySource` so measured, manual, fixed,
and non-weighed operations remain distinguishable.

## Load workflow

Load instruction creation uses four steps: contract selection, requirements,
operational instructions, and review. `Save as draft` remains editable; `Plan
load` confirms the instruction for trip generation. Existing API routes remain
compatible.

## Trailer assignment

A trip accepts zero, one, or two distinct trailers. Assignment validates active,
same-tenant resources and overlapping allocations at the service boundary; the
aggregate independently rejects a third trailer.

## Migration

`AddWorkflowWeightCaptureOptions` adds route configuration and immutable trip
snapshot fields. Existing routes and trips are migrated as requiring both
weighbridges, preserving their prior lifecycle behavior. Existing reconciliation
and rate source values are backfilled, and existing manual capture remains off.
