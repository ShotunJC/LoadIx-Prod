# LoadIx

LoadIx is a multi-tenant logistics management platform for bulk transport
operators. This repository contains the engineering foundation plus the core
domain and tenant-aware persistence model: a .NET 10 API, React 19 web shell,
tests, deterministic demo data, and a complete local dependency stack.

## Prerequisites

Install:

- .NET SDK 10.0.103 or a compatible 10.0 patch selected by `global.json`;
- Node.js 24.13.1 or a compatible Node 24 release;
- Docker Desktop with Docker Compose; and
- Git.

The database, cache, broker, object storage, and email catcher run in containers.
No local installation of those services is required.

## Start the complete stack

From the repository root in PowerShell:

```powershell
Copy-Item .env.example .env
docker compose up --build --detach --wait
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\migrate.ps1
docker compose ps
```

On macOS or Linux:

```sh
cp .env.example .env
sh scripts/start-dev.sh
sh scripts/migrate.sh
```

The PowerShell helper performs the copy, build, startup, readiness wait, and
status display:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-dev.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\migrate.ps1
```

The migration helper applies all EF Core migrations and then idempotently creates
the synthetic `LoadIx Demo Transport` tenant with two branches, three customers,
six locations, four commodities, ten drivers, ten trucks, and fifteen trailers.
It is safe to run the helper again. Development seeding is rejected outside the
`Development` environment.

The development tenant administrator is:

- email: `admin@loadix.example.invalid`
- password: the `DEMO_ADMIN_PASSWORD` value copied from `.env.example`

These are intentionally public, local-only demo credentials. Never reuse them in
a deployed environment. Invitations and password-reset messages are captured by
Mailpit at <http://localhost:8025>.

The seeded mobile driver account is:

- email: `driver@loadix.example.invalid`
- password: the `DEMO_DRIVER_PASSWORD` value copied from `.env.example`

The values in `.env.example` are public local-development defaults, not
production credentials. `.env` is ignored by Git. Replace every credential
through the deployment secret store outside local development.

### Local URLs

| Service | URL |
| --- | --- |
| LoadIx web | <http://localhost:5173> |
| API | <http://localhost:8080> |
| API liveness | <http://localhost:8080/health/live> |
| API readiness | <http://localhost:8080/health/ready> |
| System information | <http://localhost:8080/api/v1/system/info> |
| OpenAPI (development only) | <http://localhost:8080/openapi/v1.json> |
| RabbitMQ management | <http://localhost:15672> |
| MinIO console | <http://localhost:9001> |
| Mailpit | <http://localhost:8025> |

RabbitMQ and MinIO use the local values in `.env`. Mailpit captures development
email and never relays it externally.

Stop the stack without deleting data:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\stop-dev.ps1
```

Remove containers and local named volumes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\stop-dev.ps1 -RemoveVolumes
```

Volume deletion permanently removes the local SQL Server, Redis, RabbitMQ, MinIO,
and Mailpit data.

## Run API and web processes from source

Start only the dependencies:

```powershell
docker compose up --detach --wait sqlserver redis rabbitmq minio mailpit
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\migrate.ps1
dotnet run --project src/LoadIx.Api
```

In a second terminal:

```powershell
npm.cmd ci --prefix apps/loadix-web
npm.cmd run dev --prefix apps/loadix-web
```

The Vite development server proxies `/api`, `/health`, and `/openapi` to the API
at port 8080.

## Database migrations

The repository pins `dotnet-ef` as a local tool. Restore and apply migrations:

```powershell
dotnet tool restore
dotnet ef database update `
  --project src/LoadIx.Infrastructure `
  --startup-project src/LoadIx.Api
```

Apply the deterministic development seed separately when needed:

```powershell
dotnet run --project src/LoadIx.Api --no-launch-profile -- `
  --environment Development --seed-development
```

Create a reviewed migration:

```powershell
dotnet ef migrations add DescriptiveMigrationName `
  --project src/LoadIx.Infrastructure `
  --startup-project src/LoadIx.Api `
  --output-dir Persistence/Migrations
```

Production deployments must run migrations as an explicit controlled step. The
API does not mutate the production schema or seed data automatically on startup.

## Authentication security model

LoadIx has no public registration endpoint. Tenant administrators create
single-use, seven-day invitations and assign tenant-scoped roles. Access tokens
are short-lived JWTs held only in browser memory. Rotating refresh tokens are
random values held in an HttpOnly, SameSite=Strict cookie; only their SHA-256
hashes are stored in SQL Server. Production cookies are Secure and use the
`__Host-` prefix. Local HTTP development uses an unprefixed HttpOnly cookie
because browsers require `__Host-` cookies to be Secure.

Refresh-token replay revokes the complete token family. Logout, password reset,
user deactivation, and explicit session revocation invalidate stored sessions.
The web client performs one guarded refresh/retry after a 401 and then returns to
login. It never writes tokens to local storage or session storage.

Normal tenant context comes only from validated access-token claims. A platform
administrator may explicitly supply `X-LoadIx-Support-Tenant`; the API verifies
the target tenant, audits entry, enriches structured logs, and returns
`X-LoadIx-Support-Context`. Tenant users receive 403 if they attempt this.

## Master data management

After signing in, the sidebar provides management screens for branches,
customers, locations, commodities, drivers, trucks, and trailers. Lists use
server-side paging, search, status filters, deterministic sorting, and branch or
customer filters where applicable. Operators see business references such as
site codes, registrations, licence numbers, and fleet numbers instead of GUIDs.

The versioned endpoints are under:

```text
/api/v1/branches
/api/v1/customers
/api/v1/locations
/api/v1/commodities
/api/v1/drivers
/api/v1/vehicles
/api/v1/trailers
```

List responses use `{ items, page, pageSize, totalCount, totalPages }`. The
maximum page size is 100. Detail and mutation responses include a base64
`version` and an `ETag`; updates and activate/deactivate commands must return the
latest version. A stale version produces RFC Problem Details with HTTP 409.
Master-data changes are tenant-scoped, permission checked, and written to the
audit event store.

## Transport contracts and rates

Authorized commercial users manage contracts at `/contracts`. The four-step
wizard captures customer and effective dates, a source/destination route,
permitted commodity, tolerance, and a typed pricing rule. Draft contracts can be
activated only while effective and only when every route has a non-overlapping
rate. Active contracts can be suspended.

Contract APIs are under `/api/v1/contracts`. Detail and lifecycle responses
include an ETag and base64 `version`. The rate-preview endpoint returns the
calculated charge, billable tonnes, line-by-line breakdown, input snapshot, and
immutable rate snapshot. Monetary rounding defaults to two decimals with
midpoints away from zero. Fuel surcharge is applied to the subtotal before the
minimum-charge floor.

## Load instructions and planned quantities

Operations users manage customer instructions at `/load-instructions`. The
three-step wizard selects an active contract route and permitted commodity,
captures requested quantity and trip-count guidance, collection and delivery
windows, customer references, priority, and operational instructions.

Draft instructions may be edited and planned. Planned instructions can generate
trip reservations or be cancelled with a reason. The detail view displays
requested, completed, remaining, under-delivered, and over-delivered quantities,
fulfilment progress, generated trips, and audit history.

APIs are under `/api/v1/load-instructions`. Writes require the current `version`.
`POST /api/v1/load-instructions/{id}/generate-trips` additionally requires an
`Idempotency-Key` header containing 8–128 safe characters. Repeating the same
key and trip count returns the existing outcome; changing the count for a used
key returns `409 Conflict`.

Assumptions for this milestone:

- an instruction selects one route and one commodity;
- quantities are aggregated only within their explicit kilograms or tonnes unit;
- generated trips divide the current remaining quantity evenly to three decimal
  places, with the last trip absorbing the rounding remainder;
- customer references, priority, and special instructions remain editable after
  trip generation, while identity, contract, route, commodity, quantity, trip
  count, and windows are locked;
- each generated reservation is promoted to an operational trip with the same
  public identifier.

## Trip planning, dispatch, and lifecycle

Controllers manage operational trips at `/trips`. The list supports search,
status filtering, stable collection-date ordering, paging, assignment visibility,
and prominent exception states. The detail workspace provides the immutable
contract/rate snapshot, resource assignment, lifecycle controls, weighbridge
checkpoints, operational notes, assignment history, and the actor-stamped
transition timeline.

Trip APIs are under `/api/v1/trips`. Assignment and lifecycle endpoints accept
only explicit commands; there is no generic status update. Every retry-prone
command requires an `Idempotency-Key`, and every editable command requires the
latest base64 `version`. Active, available, and compliant drivers, vehicles, and
up to three trailers are checked against overlapping non-terminal trip windows.

The implemented lifecycle is:

`Planned -> Assigned -> Dispatched -> AtCollection -> Loading ->
CollectionComplete -> InTransit -> AtDelivery -> Unloading ->
DeliveryComplete -> ReconciliationRequired -> Completed`

Cancellation and rejection are reasoned terminal branches. Post-dispatch
reassignment and exceptional status overrides require `Trip.Override`, an
explicit reason, and an audit-visible override transition.

Until the dedicated Documents, Weighing, and Reconciliation modules replace
them, the trip aggregate owns structured source/destination gross, tare, net,
and ticket-reference checkpoints plus reconciliation/document readiness flags.
These are production domain facts, not external-provider adapters. Collection
and delivery cannot complete without their applicable weighbridge data, and trip
completion requires reconciliation and required-document confirmation.

## Secure operational documents

Authorized users manage trip evidence in the Documents panel on trip detail.
The responsive uploader supports drag-and-drop, mobile camera capture, progress,
retry with a stable idempotency key, safe image previews, downloads, and deletion
before trip completion. Documents become immutable when their trip is completed.

The versioned endpoints are:

```text
GET    /api/v1/documents?ownerType={type}&ownerId={id}
POST   /api/v1/documents
GET    /api/v1/documents/{id}/content
GET    /api/v1/documents/{id}/preview
DELETE /api/v1/documents/{id}
```

Uploads accept PDF, JPEG, PNG, and WebP up to 15 MB by default. The API checks
the declared size, extension, media type, binary signature, dangerous executable
markers, checksum, tenant ownership, permissions, and malware policy. It
generates opaque object keys, places content in quarantine before scanning, and
never serves original uploads inline. Image originals are preserved; a
metadata-stripped, auto-oriented, dimension-constrained JPEG preview is generated
separately.

Local object content is stored in the `MINIO_DOCUMENT_BUCKET` bucket using the
MinIO values in `.env`. SQL Server stores only document metadata and audit
records. The default development signature scanner is intentionally rejected
outside the Development environment. A production deployment must register and
configure an approved `IMalwareScanner` adapter, then set
`Documents__MalwareScanner` accordingly; uploads fail closed while that provider
is unavailable. The `IObjectStorage` port keeps business logic independent from
MinIO and can be implemented for an S3-compatible provider or Azure Blob.

Apply the document schema migration with the normal migration command:

```powershell
dotnet ef database update `
  --project src/LoadIx.Infrastructure `
  --startup-project src/LoadIx.Api
```

## Driver mobile PWA

Drivers use `/driver`, a touch-first workspace that shows only trips assigned to
their linked driver profile. It provides next-trip visibility, collection and
delivery directions, legal lifecycle actions, weighbridge capture,
camera-friendly ticket/POD upload, notes, and an explicit location-update action.
Location is requested only after the driver presses the action; denial does not
block the workflow and the web app never attempts covert background tracking.

The production build registers a service worker that caches only the application
shell and static assets. API responses and access tokens are not cached. Offline
trip commands are held in IndexedDB with client-generated idempotency keys,
backoff retry, and visible pending/failed/conflict state. Commands older than
seven days require driver review rather than being silently lost; logout clears
the local queue. Browser background sync/geolocation and guaranteed camera-upload
continuation are not portable browser capabilities, so any future tracking or
native integration must be approved and implemented explicitly.

## Operations control tower

Controllers use `/operations` for a live, tenant-scoped board. The SignalR hub
uses the authenticated tenant claim rather than a browser-supplied group. If a
websocket reconnects or is unavailable, the board shows its connection state and
falls back to refresh/polling. Map tiles use MapLibre's demonstration style only
for local development; choose a licensed OpenStreetMap-compatible provider before
production.

The board groups active trips by operational stage, supports search plus
customer, branch, driver, vehicle, commodity, delayed-only, and exception-only
filters, and displays latest known positions with freshness labels. Stale
positions are deliberately shown as stale, not live.

## Notifications

The notification centre is available from the header and `/notifications`.
Users can mark notifications read, update channel preferences, and tenant
administrators can preview/edit tenant templates. Users with audit access can
inspect failed deliveries and queue retries.

Notification requests are stored in SQL Server with deduplication keys and
channel delivery rows. A hosted worker sends only committed rows, records
attempts, provider correlation IDs, failure reasons, retry timing, and audit
events. Local email uses Mailpit through SMTP. Local SMS uses the explicit
development adapter and never leaves the machine; production SMS requires a real
`ISmsNotificationSender` implementation and provider configuration.

## Financial calculation and invoice preparation

Finance users use `/finance` plus the Financials tab on trip detail to prepare
customer charges. Trip revenue is calculated only for completed, reconciled
trips with required documents and a reviewed tenant tax configuration. The
calculation uses the trip's immutable contract-rate snapshot, billable tonnes,
explicit tax treatment, and decimal arithmetic; completed and invoiced records
are not silently recalculated.

The versioned endpoints are:

```text
GET  /api/v1/finance/tax-configuration
PUT  /api/v1/finance/tax-configuration
GET  /api/v1/finance/trips/{tripId}
POST /api/v1/finance/trips/{tripId}/recalculate
POST /api/v1/finance/trips/{tripId}/costs
POST /api/v1/finance/trips/{tripId}/approve
GET  /api/v1/finance/invoice-ready
POST /api/v1/finance/invoice-batches/preview
POST /api/v1/finance/invoice-batches
GET  /api/v1/finance/invoice-batches
POST /api/v1/finance/invoice-batches/{id}/approve
POST /api/v1/finance/invoice-batches/{id}/mark-exported
GET  /api/v1/finance/invoice-batches/{id}/export.csv
GET  /api/v1/finance/invoice-batches/{id}/summary.pdf
```

Cost entries support driver cost, fuel estimate/actual, tolls, subcontractor,
weighbridge, loading, unloading, and approved miscellaneous costs. Adding costs
requires the latest financial `version`; post-approval or post-invoice
corrections must be explicit adjustment entries. Invoice batches group selected
approved trips by customer, contract, and currency, prevent duplicate
invoicing, support finance approval, and export CSV/PDF summaries for handoff.
No accounting-platform posting is performed in this milestone.

## Verify the repository

Backend:

```powershell
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore --warnaserror
dotnet test --no-build
```

Frontend:

```powershell
npm.cmd ci --prefix apps/loadix-web
npm.cmd run format:check --prefix apps/loadix-web
npm.cmd run lint --prefix apps/loadix-web
npm.cmd run typecheck --prefix apps/loadix-web
npm.cmd run test --prefix apps/loadix-web
npm.cmd run build --prefix apps/loadix-web
Push-Location apps/loadix-web
npx.cmd playwright install chromium
npm.cmd run e2e
Pop-Location
```

Containers:

```powershell
docker compose config
docker compose build
docker compose up --detach --wait
```

## Local HTTPS

Compose intentionally uses HTTP on loopback; production TLS terminates at the
approved ingress/reverse proxy. For local HTTPS testing of the API:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/LoadIx.Api --launch-profile https
```

Then use <https://localhost:8081>. Do not commit development certificates or
private keys. If the web client is also served over HTTPS, update
`LoadIx__WebOrigin` to the exact trusted origin.

## Demo VPS deployment with Docker

For a temporary demonstration server, use the public overlay. It places Caddy
in front of the web container, obtains a certificate for the configured domain,
and keeps SQL Server, Redis, RabbitMQ, MinIO, and Mailpit off the public network.
This overlay retains the Development environment and synthetic seed accounts;
do not use it for customer or production data.

On an Ubuntu VPS with Docker Compose v2:

```sh
git clone <repository-url> LoadIx-Prod
cd LoadIx-Prod
cp .env.example .env
sed -i 's/^DEMO_DOMAIN=.*/DEMO_DOMAIN=crimsonskyit.co.za/' .env
```

Replace every local password in `.env`, especially `SQL_PASSWORD`,
`RABBITMQ_PASSWORD`, `MINIO_PASSWORD`, `JWT_SIGNING_KEY`,
`DEMO_ADMIN_PASSWORD`, `DEMO_DRIVER_PASSWORD`, and `SMTP_PASSWORD`.
The demo overlay sends account emails through authenticated SMTP at
`mail.crimsonskyit.co.za:587` using STARTTLS. Point the domain's DNS A record at
the VPS before starting Caddy. Allow inbound TCP 80 and 443 (and SSH from your
administration address) in the VPS firewall.

Start the demo stack:

```sh
docker compose -f compose.yaml -f compose.demo.yaml up --build --detach --wait
```

Apply the schema and deterministic demo seed with the one-shot Docker migration
container. It must not be used for a production deployment:

```sh
docker compose -f compose.yaml -f compose.demo.yaml --profile tools run --rm migrate
```

If you prefer to run migrations from the host, the existing helper requires the
.NET 10 SDK. If you changed `SQL_PASSWORD`, export the same connection string
before running it because `.env` is read by Compose, not automatically by the
.NET tooling:

```sh
export SQL_PASSWORD='the same value used in .env'
export ConnectionStrings__Database="Server=127.0.0.1,1433;Database=LoadIx;User Id=sa;Password=$SQL_PASSWORD;Encrypt=False;TrustServerCertificate=True"
./scripts/migrate.sh
```

Open `https://crimsonskyit.co.za`. Check the stack with:

```sh
docker compose -f compose.yaml -f compose.demo.yaml ps
docker compose -f compose.yaml -f compose.demo.yaml logs --tail=100 api web caddy
```

Users with `Report.View` can open **Reports** in the dashboard or call the
tenant-scoped API at `/api/v1/reports/dashboard` and
`/api/v1/reports/{report}`. Supported report names are documented in the
reporting endpoint OpenAPI description. Use date-only `from` and `to` query
values; the API resolves them in the tenant timezone with an exclusive upper
boundary. Small CSV exports use `/export.csv` and are limited to 100 matching
rows; larger exports should be queued with `POST /export-jobs`, then polled and
downloaded from the returned job URL. Jobs are persisted in SQL Server and
delivered through the durable RabbitMQ `loadix.report-exports` queue; a
republisher retries queued or lease-expired work. Every response includes a
freshness timestamp and effective tenant timezone.

The MinIO and Mailpit consoles are intentionally private in this overlay. Use
an SSH tunnel when needed, for example `ssh -L 9001:127.0.0.1:9001 user@server`,
or inspect them only from the VPS. Keep regular backups of the named volumes.

## Repository layout

```text
src/       .NET API, application, domain, infrastructure, and contracts
tests/     unit, integration, and architecture tests
apps/      React web application and Playwright tests
deploy/    API/web Dockerfiles and Nginx configuration
scripts/   local startup, shutdown, and migration helpers
docs/      product, architecture, glossary, workflow, and milestone guidance
```

Read [AGENTS.md](AGENTS.md) and the documents under [docs](docs) before making
changes.

## Troubleshooting

- If a host port is occupied, change it in `.env`, then recreate the stack.
- If readiness is unhealthy, run `docker compose ps` and
  `docker compose logs api sqlserver redis rabbitmq minio mailpit`.
- If SQL Server retained an old local password, remove the development volume
  with `.\scripts\stop-dev.ps1 -RemoveVolumes` and start again.
- If PowerShell prevents `npm.ps1`, use `npm.cmd` as shown above.
