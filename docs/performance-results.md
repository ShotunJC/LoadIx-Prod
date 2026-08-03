# Milestone 17 performance and readiness evidence

This document is the reproducible record for the Milestone 17 performance
work. It separates implemented safeguards and test harness thresholds from
measurements that have actually been run. A threshold in the k6 script is not a
performance claim until a run is recorded here.

## Generated workload

The explicit performance seed creates fictional data in a fresh local database:

| Entity | Count |
| --- | ---: |
| tenants | 10 |
| users | 100 |
| drivers | 1,000 |
| vehicles | 1,000 |
| load instructions | 10,000 |
| trips | 100,000 |
| trip assignments | up to 100,000 |
| trip transitions/events | 100,000 |
| trip location updates | 100,000 |
| trip weighbridge measurements | represented on the trip rows |
| dispatched operational outbox messages | 100,000 |

The normal development seed is unchanged. Performance data is only created by
the explicit command below, and reseeding a database containing the generated
tenant names fails closed.

## Exact setup commands

Run from the repository root in PowerShell. These commands use the local Compose
stack and do not place a performance password in source control.

~~~
Copy-Item .env.example .env -Force
docker compose up --build --detach --wait
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\migrate.ps1
$env:LOADIX_PERFORMANCE_SEED_PASSWORD = Read-Host "Performance seed password (12+ characters)"
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\seed-performance.ps1
~~~

The seeder runs migrations as a safety check, creates the ten tenant roles and
users, and bulk-loads the operational rows. It must not be added to normal API
startup or to the ordinary development seed path.

The generated administrator for tenant 01 is
perf-admin-01@loadix.example.invalid. The seed links that administrator to
tenant 01's first generated driver so the protected location-update scenario can
be exercised. Obtain a suitable assigned trip ID before running k6:

~~~
$perfPassword = Read-Host "Performance admin password"
$loginBody = @{ email = 'perf-admin-01@loadix.example.invalid'; password = $perfPassword } | ConvertTo-Json
$login = Invoke-RestMethod -Uri 'http://localhost:8080/api/v1/auth/login' -Method Post -ContentType 'application/json' -Body $loginBody
$tripPage = Invoke-RestMethod -Uri 'http://localhost:8080/api/v1/trips/?status=Assigned&page=1&pageSize=20' -Headers @{ Authorization = "Bearer $($login.accessToken)" }
$env:PERF_EMAIL = 'perf-admin-01@loadix.example.invalid'
$env:PERF_PASSWORD = $perfPassword
$env:PERF_TRIP_ID = $tripPage.items[0].id
~~~

## Load-test command

The maintainable k6 scenario is perf/k6/loadix.js. It covers login, trip list,
operations board, status transition, location update, weighbridge submission,
SignalR connection, and report request. Commands accept HTTP 409 for the
mutation scenarios because that is the expected optimistic concurrency response
when two test iterations select the same trip; the latency sample is still
retained and the check makes the conflict visible.

With k6 installed locally:

~~~
k6 run .\perf\k6\loadix.js
~~~

With Docker Desktop (the current local environment did not have k6 installed):

~~~
$loadTestPath = (Get-Location).Path
docker run --rm -i -v "$loadTestPath\perf\k6:/scripts:ro" -e BASE_URL=http://host.docker.internal:8080 -e HOST_HEADER=localhost -e PERF_EMAIL=$env:PERF_EMAIL -e PERF_PASSWORD=$env:PERF_PASSWORD -e PERF_TRIP_ID=$env:PERF_TRIP_ID grafana/k6:latest run /scripts/loadix.js
~~~

Record the complete k6 summary, the git commit, the database engine/container
versions, the host CPU/RAM, and whether the run used a warm or cold cache. Do
not replace a failed run with a manually edited result.

## Target result record

The targets are:

| Scenario | Target | Result |
| --- | ---: | --- |
| normal read API (trip list and board) | p95 < 500 ms | Measured local run: 86.64 ms and 97.99 ms |
| normal command API (status, location, weighbridge) | p95 < 800 ms | Measured local run: 75.83 ms, 68.76 ms, and 72.15 ms |
| operations board initial load | < 3 s | Measured local run: 97.99 ms at a 250-row response limit |
| paginated API memory behavior | SQL Skip/Take, no full-table materialization | Verified in code and reviewed against the generated database plan |
| large report export | background execution | Implemented; enqueue and end-to-end worker latency must be remeasured with the corrected k6 scenario |

### Measured local k6 run — 2026-08-03

This was one warm local Compose run against the generated tenant workload. The
profile ran for 30 seconds with 10 one-shot login VUs, 2 trip-list iterations
per second, and 1 iteration per second for each other scenario. It completed
254 iterations, 317 HTTP requests, 30 SignalR sessions, and 325/325 checks with
0% HTTP failures. These measurements are evidence for this environment and
profile only; they are not a production capacity claim.

| Scenario | p95 |
| --- | ---: |
| login | 219.41 ms |
| trip list | 86.64 ms |
| operations board | 97.99 ms |
| status transition | 75.83 ms |
| location update | 68.76 ms |
| weighbridge submission | 72.15 ms |
| synchronous report read (legacy scenario) | 124.28 ms |
| SignalR session | 1.01 s |

The final Docker command was:

~~~
$loadTestPath = (Get-Location).Path
docker run --rm -i -v "$loadTestPath\perf\k6:/scripts:ro" -e BASE_URL=http://host.docker.internal:8080 -e HOST_HEADER=localhost -e PERF_EMAIL=$env:PERF_EMAIL -e PERF_PASSWORD=$env:PERF_PASSWORD -e PERF_TRIP_ID=$env:PERF_TRIP_ID grafana/k6:latest run /scripts/loadix.js
~~~

### Database runtime review — 2026-08-03

The generated database was warm from the local k6 run. Representative SQL
statistics were captured with `SET STATISTICS IO ON` and `SET STATISTICS TIME ON`;
all physical reads were zero except five workfile reads during the board query.
The corresponding `SHOWPLAN_TEXT` review found seeks rather than table scans on
the tenant-scoped operational paths:

| Query shape | Runtime evidence | Plan review |
| --- | --- | --- |
| trip list, 50 rows | 337 `Trips` logical reads; 13 ms CPU; 34 ms elapsed | Seeks on `IX_Trips_TenantId_Status_PlannedCollectionAtUtc_Id`; the optimizer used index intersection and a bounded sort for the projection |
| operations board, 250 rows | 517 `Trips`, 500 `Customers`, and 500 `Commodities` logical reads; 23 ms CPU; 76 ms elapsed | Nested-loop joins with tenant-scoped seeks and a bounded `ORDER BY` sort; no table scan |
| latest locations, up to 5,000 rows | 346 `TripLocationUpdates` and 160 `Trips` logical reads; 18 ms CPU; 17 ms elapsed | Seeks on both tenant/date and tenant/trip location indexes, followed by a stream aggregate for the latest row per trip |
| report cost aggregation | The generated workload has no cost-entry rows; 2 `TripFinancials` logical reads | `TripFinancials` index scan plus tenant/trip-financial cost seeks; rerun with representative financial rows before pilot sign-off |

The captured SQL was run with the following command shape (the password is
provided interactively or through the local environment and is not committed):

~~~
$sqlPassword = Read-Host "SQL Server password"
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -d LoadIx -U sa -P $sqlPassword -C -Q "SET STATISTICS IO ON; SET STATISTICS TIME ON; SELECT TOP (250) Id, TenantId, Status, PlannedDeliveryAtUtc FROM trips.Trips WHERE TenantId = '<tenant-id>' AND Status IN ('Assigned','InTransit','ReconciliationRequired') ORDER BY PlannedCollectionAtUtc, Id;"
~~~

These are local plan and statistics observations, not a capacity claim. The
board still performs a sort for the final projection, and full financial report
plans need a populated cost/financial workload before deployment sizing.

### Current environment record — 2026-08-03

- host: Windows 10 build 19045, PowerShell, AMD Ryzen 7 7700X (16 logical
  processors), 31.1 GiB RAM;
- .NET SDK: 10.0.103;
- Node.js: 24.13.1;
- Docker Engine: 29.2.0;
- Docker Compose: v5.0.2;
- k6: Docker image `grafana/k6:latest`, pulled as
  `sha256:e7eeddf1ce2361df6920d925297f487c0ba549c44be242c6a9c22f28d9b08efa`;
- local Compose smoke test: `docker compose up --build --detach --wait` completed
  with SQL Server, Redis, RabbitMQ, MinIO, Mailpit, API, and web healthy; the
  web root and proxied readiness endpoint both returned HTTP 200;
- generated-data validation: `scripts/seed-performance.ps1` completed in about
  34 seconds and the tenant-scoped counts matched the requested workload;
- k6 latency run: completed using the command recorded above; the detailed
  result is recorded in the table above;
- full .NET suite: the 15-minute bounded run timed out while Testcontainers was
  still initializing its per-fixture SQL databases; focused integration suites
  for operations/notifications (2), trips (3), contracts/documents/load
  instructions (10), and authentication (12) passed.

This is a local evidence record, not a production capacity guarantee. A pilot
gate still requires failure/recovery drills and a run under the intended
deployment topology.

The recorded run predates the corrected background-report and SignalR protocol
checks now present in `perf/k6/loadix.js`. Its 124.28 ms report value measured a
synchronous trip-register read and must not be used as evidence for background
export enqueue latency. Rerun k6 before pilot sign-off and append the unedited
summary; do not overwrite the historical measurement.

## Query-plan and failure checks

The local board, trip-list, latest-location, and report-cost query review is
recorded above. To repeat it against another generated database, capture SQL
Server execution plans and IO/time statistics with the same SQL credentials as
the Compose stack and keep the output with the run artifact:

~~~
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -d LoadIx -U sa -P $env:SQL_PASSWORD -C -Q "SET STATISTICS IO ON; SET STATISTICS TIME ON; SELECT TOP (250) Id, TenantId, Status, PlannedDeliveryAtUtc FROM trips.Trips WHERE TenantId = '<tenant-id>' AND Status IN ('Assigned','InTransit','ReconciliationRequired') ORDER BY PlannedCollectionAtUtc, Id;"
~~~

Review the actual execution plan for scans, spills, excessive key lookups, and
sorts. Also verify that page-size bounds hold under a large requested value and
that the locations endpoint does not return more than its bounded response.

Reliability drills should include stopping and restarting SQL Server, RabbitMQ,
MinIO, and the API during an outbox/report/notification operation; replaying the
same idempotency key; forcing a stale row-version command; and disconnecting a
SignalR client. Record recovery time, retry count, dead-letter state, and the
final API-visible outcome. These drills were not run in the current workspace.

## Known bottlenecks to investigate during the pilot run

- SQL Server remains the critical dependency for tenant-scoped operational and
  reporting queries; the local plans selected the new status/date and latest
  location indexes, but the review should be repeated with pilot-shaped
  financial rows.
- Operations board joins are bounded by request limit, but a tenant-wide board
  with a high limit still has a larger result and should be measured separately.
- Report exports are bounded and backgrounded, but completed content is currently
  stored in varbinary(max); object-backed export artifacts should be considered
  if pilot exports approach the configured 100,000-row limit.
- Outbox and notification workers are durable and back off, but multi-replica
  lease/claim behavior should be exercised before running more than one worker
  replica.
- Metrics are exported through OpenTelemetry; alert thresholds and a concrete
  telemetry backend remain deployment decisions.
