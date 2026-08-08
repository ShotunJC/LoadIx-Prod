# LoadIx Pilot Demonstration Guide

## Purpose and safety

The `LoadIx Demo Transport` tenant is a deterministic sales and pilot-training
environment. Every company, person, reference, address and document is synthetic.
No logo is embedded and no record represents a real person or organisation.

The reference instant is `2026-08-07T10:00:00Z`. Stable record identifiers,
references, weights and financial inputs make screenshots and training outcomes
repeatable. The demo contains two branches, five customers, eight locations,
five commodities, twenty drivers, fifteen trucks, twenty-five trailers, four
active contracts, eight load instructions and thirty-two trips across eleven
workflow statuses. Six completed trips are approved and invoice-ready.

## Create or reset the demo

Apply migrations first. Seed without changing an existing demo:

```powershell
.\scripts\demo-data.ps1
```

Restore the operational tenant to its canonical state:

```powershell
.\scripts\demo-data.ps1 -Reset
```

On macOS or Linux use `sh scripts/demo-data.sh` or
`sh scripts/demo-data.sh --reset`. The reset removes and recreates only the
well-known synthetic tenant's operational/master data and object-store samples.
Its immutable security audit evidence and stable login identities remain intact.

The API rejects both commands unless all of these conditions hold:

- ASP.NET Core environment is `Development`;
- `LOADIX_DEMO_SEED_ENABLED` is exactly `true`;
- the database has a non-empty catalog name that is neither `master` nor
  production-like.

The demo Compose tool profile supplies these controls. Runtime containers remain
in `Production` and never seed automatically.

## Accounts

- Administrator: `admin@loadix.example.invalid`
- Driver: `driver@loadix.example.invalid`
- Passwords: the local `DEMO_ADMIN_PASSWORD` and `DEMO_DRIVER_PASSWORD` values
  from `.env`; never reuse them outside a disposable demonstration environment.

## Demonstration scripts

### 1. Create a customer contract

1. Sign in as the administrator and open **Contracts**.
2. Select **New contract** and choose **Training Infrastructure**.
3. Enter a unique synthetic contract number, a current effective period, ZAR,
   payment terms and a 1% weight tolerance.
4. Select **Demo Mine North** to **Midrand Customer Site**, permit **Aggregate**,
   and add a reconciled-tonne rate.
5. Save, review the commercial summary, then activate the contract.
6. Point out that effective dates, route, commodity, rounding and tolerance are
   explicit and become immutable snapshots on trips.

### 2. Plan a load instruction

1. Open **Load instructions** and select **New instruction**.
2. Choose an active demo contract and its route and commodity.
3. Enter 136 tonnes, four trips, collection/delivery windows and a fictional PO.
4. Save the draft, plan it, then generate four trips.
5. Show the requested, planned and remaining quantities and the generated
   `LI-DEMO-...-T...` references.

### 3. Dispatch a trip

1. Open **Trips**, filter status to **Planned**, and open a demo trip.
2. Assign an available Demo Driver, truck and trailer.
3. Dispatch the trip and confirm the status and audit timeline update.
4. Open **Operations** to show the card moving to **Dispatched**.

### 4. Driver collection workflow

1. Sign in as `driver@loadix.example.invalid` on a narrow/mobile viewport.
2. Open **Driver workspace** and select the assigned trip.
3. Mark arrival at collection and start loading.
4. Toggle browser offline mode, queue one permitted update, then reconnect.
5. Show the explicit queued/sync state and that retry does not duplicate the
   milestone.

### 5. Weighbridge capture

1. From the active trip, capture source gross and tare weights and a synthetic
   `SRC-DEMO-...` ticket reference.
2. Attach a collection weighbridge ticket if demonstrating upload validation.
3. Confirm the calculated net weight and complete collection.
4. Explain that measured quantities use fixed precision and evidence remains
   tenant-authorised.

### 6. Track the trip

1. Depart collection and open **Operations**.
2. Filter to **In transit**, then open the map/list view.
3. From Driver workspace, submit a permitted location update.
4. Show reconnect/refetch status and tenant-scoped live updates.

### 7. Delivery

1. Mark arrival at delivery and start unloading.
2. Capture destination gross/tare values and `DST-DEMO-...` reference.
3. Upload proof of delivery or open one of the repository-generated synthetic
   PDF proofs on a completed trip.
4. Complete delivery and submit the trip for reconciliation.

### 8. Reconciliation within tolerance

1. Open a `ReconciliationRequired` trip whose destination net differs by about
   0.120 tonnes.
2. Compare source/destination net values and the contract tolerance.
3. Confirm reconciliation, verify required documents, and complete the trip.
4. Show the immutable rate snapshot and resulting finance record.

### 9. Reconciliation exception and approval

1. Open an exception example (seeded references include a 1.250 tonne variance).
2. Review the highlighted variance and supporting ticket/POD context.
3. Record a specific reason and use the authorised approval/override path.
4. Show the operational note, audit history and exception reporting; emphasize
   that approval records the decision rather than changing captured weights.

### 10. Invoice batch creation

1. Open **Finance** and filter to approved, invoice-ready trips.
2. Select trips for one customer and contract; the canonical seed provides six
   approved candidates across its completed work.
3. Create a draft batch, review billable tonnes, tax treatment, revenue and cost.
4. Approve the batch and export the integration-ready payload if appropriate.
5. Clarify that LoadIx prepares invoice data; it does not issue a SARS tax invoice.

### 11. Management reporting

1. Open **Reports** and review the KPI cards.
2. Run **Trip register**, **Source versus destination variance**, **Contract
   performance**, **Trip profitability** and **Invoice status**.
3. Filter by branch/customer and demonstrate stable pagination.
4. Export a current page or queue a larger CSV export.
5. Explain the tenant-local date interpretation and ZAR formatting.

### 12. Simulated edge-device reading

1. Open **Edge devices** and inspect **Central Weighbridge Demo Agent** and its
   healthy heartbeat at **Central Weighbridge**.
2. From a terminal run the documented `LoadIx.EdgeSimulator` bootstrap flow for
   that local environment, then submit a generic stable line such as
   `ST,GS,45670,kg`.
3. Disconnect the simulator, capture another reading, reconnect and replay it.
4. Show the device diagnostics and reading history, explaining timestamp,
   nonce, idempotency and credential checks at the edge boundary.

## Presenter reset checklist

1. Run the reset command and confirm it exits successfully.
2. Sign in with both accounts once.
3. Confirm **Dashboard**, **Operations**, **Finance**, **Notifications** and
   **Edge devices** contain data.
4. Open one generated PDF and verify download authorisation.
5. Keep all changes synthetic; reset after each training cohort.
