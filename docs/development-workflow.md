# LoadIx Development Workflow

## Current Repository Assessment

The repository started as an empty Git repository. Milestone 1 established the
.NET 10 solution, React 19 application, containerized development dependencies,
tests, formatting/linting rules, and GitHub Actions CI. `README.md` is the
canonical quick start; this document describes the contribution workflow.

## Prerequisites for the Planned Stack

The foundation pins or targets:

- .NET 10 SDK
- Node.js 24.13.1 (or a compatible Node 24 release) and npm
- Docker Desktop or compatible Docker Engine with Compose
- Git
- PowerShell 7 or a POSIX-compatible shell

Developers should not need locally installed SQL Server, RabbitMQ, Redis, or
MinIO; Compose supplies development dependencies.

## Configuration

- Commit `.env.example` containing names and safe development defaults, never
  secrets.
- Keep local secrets in user secrets or an ignored `.env` file.
- Bind backend settings through validated options and fail fast for missing
  required production configuration.
- Prefix environment variables consistently, using .NET's `Section__Property`
  convention.
- Make development seed credentials explicit local-only values that cannot run in
  production.
- Keep provider credentials in the deployment platform's secret store.

## Branch and Change Flow

1. Start from an up-to-date main branch and create a focused branch.
2. Read `AGENTS.md`, relevant docs, and nearby code/tests.
3. Confirm the milestone and write a short implementation plan.
4. Make the smallest cohesive change, preserving module boundaries.
5. Add unit, integration, architecture, component, or end-to-end tests at the
   lowest useful level.
6. Add migrations, seed updates, OpenAPI changes, and docs as part of the same
   change.
7. Run local quality gates.
8. Review the diff for unrelated changes, secrets, generated noise, and unsafe
   logging.
9. Commit with a message that describes the outcome.
10. Use pull-request review and passing CI before merging.

Prefer small vertical slices that deliver a complete behavior over large batches
of disconnected layer scaffolding.

## Planned Local Bootstrap

These commands describe the implemented developer experience:

```powershell
Copy-Item .env.example .env
docker compose up --build --detach --wait
.\scripts\migrate.ps1
```

The migration helper also applies deterministic, development-only demo data.
For host-run API/web processes and exact migration/seed commands, see
`README.md`.

Mailpit captures invitation, email-confirmation, and password-reset messages in
local development. Authentication secrets or raw tokens must never appear in
logs, screenshots, fixtures, committed configuration, or support bundles.

## Quality Gates

### Backend

```powershell
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
dotnet test --no-build
```

- Unit tests cover domain invariants and pure calculation policies.
- Integration tests use Testcontainers for SQL Server and, when relevant,
  RabbitMQ, Redis, and MinIO behavior.
- Architecture tests enforce project dependency direction and forbidden
  cross-module references.
- Tenant isolation tests create at least two tenants and attempt reads and writes
  through real application/API paths.
- Tests use `TimeProvider` and deterministic IDs/data where repeatability matters.

### Frontend

```powershell
npm run format:check --prefix apps/loadix-web
npm run lint --prefix apps/loadix-web
npm run typecheck --prefix apps/loadix-web
npm run test --prefix apps/loadix-web
npm run build --prefix apps/loadix-web
npm run e2e --prefix apps/loadix-web
```

Component tests verify behavior and accessibility rather than implementation
details. Playwright covers a small number of high-value user journeys against the
real API and containerized dependencies.

### Containers and contracts

```powershell
docker compose config
docker compose build
```

CI should start the stack, wait on health checks, apply migrations, run smoke/E2E
checks, and always collect diagnostics on failure. OpenAPI generation should be
deterministic; CI fails when the checked-in client/contract is stale.

## Database Changes

- Every schema change uses an EF Core migration with a descriptive name.
- Review generated SQL and the rollback/forward-fix strategy.
- Avoid long table locks and non-null additions without a backfill/default plan.
- Add indexes for observed/common tenant-status-time queries, not speculative
  indexing.
- Never edit an already released migration. Add a corrective migration.
- Seed only reference data and clearly synthetic development/demo records.
- Production migration execution is an explicit deployment step, not an
  uncontrolled application startup side effect.

## API Changes

- Keep public endpoints under a versioned route.
- Update OpenAPI and generated TypeScript clients with the implementation.
- Classify breaking changes and use a deprecation/migration plan.
- Include authorization, validation, pagination bounds, stable sorting,
  concurrency, idempotency, and Problem Details in endpoint acceptance tests.

## Package Policy

Before adding a package:

1. confirm the platform/framework does not already provide the capability;
2. verify compatibility with the pinned runtime and license suitability;
3. check maintenance, security history, and transitive dependency cost;
4. state why the package is needed in the pull request; and
5. centralize backend versions in `Directory.Packages.props` and commit the
   frontend lockfile.

Initial package choices must be made during scaffolding and recorded in an ADR
when they create an architectural dependency. MediatR and TanStack Router remain
explicit selections, not automatic assumptions.

## Test Data and Privacy

- Use generated fictional names, phone numbers, registrations, documents, and
  coordinates.
- Never copy production or personal information into development, tests,
  screenshots, logs, or support bundles.
- Keep test tenant IDs explicit so cross-tenant scenarios are easy to understand.
- Test data builders should default to valid states but allow focused invalid
  cases.

## Observability During Development

- Use structured logs and a trace ID across API, database, job, and message paths.
- Run an OpenTelemetry-compatible local collector/backend when introduced.
- Redact tokens, passwords, raw document data, and unnecessary personal data.
- Treat audit events as tested domain outputs, not incidental log messages.

## Pull Request Handoff

Every handoff should state:

- behavior delivered and boundaries affected;
- material design choices and assumptions;
- migrations, configuration, and new dependencies;
- tests and commands run with results;
- screenshots for meaningful UI changes;
- operational/deployment implications; and
- remaining risks or intentionally deferred work.

## CI Plan

The GitHub Actions pipeline contains or is expected to contain:

1. repository hygiene and secret scanning;
2. backend restore, formatting, warning-as-error build, unit and architecture tests;
3. frontend clean install, formatting, lint, typecheck, component tests, and build;
4. container-backed integration tests;
5. production Docker image builds for API, web, worker, and migration images;
6. dependency and container vulnerability scanning;
7. post-deployment HTTPS smoke tests on a protected pilot environment;
8. a manually approved database migration job before application traffic; and
9. publication only from protected, approved branches with immutable image tags.

The repository workflows are split into `ci.yml` (backend/frontend build,
format, tests, lint, typecheck, and build), `security.yml` (dependency and
container scans), `containers.yml` (production image build and protected GHCR
publication), `pilot.yml` (protected SSH migration, dedicated-server Compose
deployment, smoke test, and failure guidance), and `rollback.yml` (protected
manual image rollback). No workflow stores a cloud credential or deploys merely
because a developer pushes a branch.

Dependency caching may accelerate builds but must not replace lockfile-based clean
installs.
