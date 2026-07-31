# LoadIx Repository Guidance

This file applies to the entire repository. More specific `AGENTS.md` files may be
added below a module only when that module needs additional rules; they must not
weaken the controls defined here.

## Product Context

LoadIx is a multi-tenant logistics management SaaS for bulk transport operators,
initially focused on South Africa. The system manages work from contract and load
instruction through dispatch, collection, delivery, reconciliation, proof of
delivery, invoice preparation, and profitability reporting.

Read these documents before changing the system:

- `docs/product-scope.md`
- `docs/architecture.md`
- `docs/domain-glossary.md`
- `docs/development-workflow.md`
- `docs/milestones.md`

## Working Rules

1. Inspect the repository and relevant documentation before implementing a
   milestone.
2. Keep changes within the requested scope and preserve unrelated work.
3. Write a brief implementation plan before coding.
4. Put business rules in the domain or application layer, never in controllers,
   endpoint handlers, React components, or persistence adapters.
5. Treat tenant isolation, authorization, auditability, and optimistic
   concurrency as acceptance criteria, not later enhancements.
6. Add or update tests with production code. Never weaken an assertion merely to
   make a build pass.
7. Run formatting, builds, tests, and applicable container checks before handing
   off work.
8. Update OpenAPI, migrations, seed data, and documentation when a change affects
   them.
9. Record material architectural decisions in `docs/architecture.md` or a
   focused architecture decision record under `docs/decisions/`.
10. Document assumptions rather than silently inventing business policy.

## Architecture Boundaries

- Build a modular monolith. Do not introduce independently deployed services
  without an approved architecture decision.
- Keep the dependency direction:
  `Api -> Application -> Domain`, with `Infrastructure` implementing interfaces
  owned by the inner layers. `Contracts` contains versioned integration and
  transport contracts, not domain behavior.
- Modules may collaborate through explicit application interfaces or domain/
  integration events. Do not reach into another module's database configuration,
  internal entities, or repositories.
- Prefer feature-oriented folders inside projects so a business capability can
  later be extracted without reorganizing the entire solution.
- Avoid a generic repository abstraction over Entity Framework Core. Use
  use-case-specific query/command abstractions where a boundary is valuable and
  use the EF Core context directly inside infrastructure-backed application
  implementations where appropriate.
- Keep controllers/endpoints thin: transport validation, application dispatch,
  and HTTP result mapping only.

## Mandatory Engineering Controls

- Enable nullable reference types.
- Use `decimal` for money and measured quantities that require fixed precision.
- Store timestamps as UTC and obtain time through `TimeProvider`.
- Use `Guid` identifiers for externally visible records.
- Accept a `CancellationToken` on asynchronous application and infrastructure
  APIs.
- Enforce maximum page sizes and deterministic secondary sorting on every
  paginated list.
- Add optimistic concurrency to editable operational and financial records.
- Never accept tenant identity from client input as authority. Resolve it from the
  authenticated principal and trusted request context.
- Every tenant-owned table must carry `TenantId`; unique constraints and common
  query indexes must include it.
- Test cross-tenant denial explicitly for every new tenant-owned aggregate or
  endpoint.
- Use RFC 9457 Problem Details for API errors.
- Use idempotency for retry-prone mobile and integration commands.
- Validate uploads by size, declared type, extension, and file signature. Store
  document metadata separately from object content and authorize every download.
- Do not log secrets, credentials, tokens, identity documents, or file contents.
- Do not commit secrets or production-like personal data.
- Do not add fake production adapters. Test doubles belong in test projects.
- Avoid catch-all exception handling unless the exception is rethrown or mapped
  centrally with appropriate telemetry.
- Do not scatter `DateTime.Now` or `DateTime.UtcNow` through application code.

## Backend Conventions

- Prefer explicit commands, queries, and handlers organized by feature.
- Domain entities protect invariants with constructors/factories and behavior
  methods; persistence-only setters should not become public mutation APIs.
- Domain events describe facts that occurred. Integration events are durable,
  versioned contracts published through an outbox.
- Transactions end at an aggregate/use-case boundary. Avoid distributed
  transactions.
- Use EF Core migrations for schema changes; migrations are reviewed artifacts.
- Apply authorization in the application use case even when an endpoint also has
  a policy attribute.
- Return typed results from application handlers; do not throw exceptions for
  expected validation or conflict outcomes.

## Frontend Conventions

- Keep server state in TanStack Query and local form state in React Hook Form.
- Use Zod schemas at untrusted boundaries and keep API types generated or mapped
  from the OpenAPI contract.
- Components render and coordinate interaction; business workflow decisions live
  in feature services/hooks or the backend.
- Implement loading, empty, error, stale/offline, and success states.
- Meet WCAG 2.2 AA where practical, including keyboard operation, focus
  visibility, semantic labeling, and accessible contrast.
- Driver workflows must tolerate intermittent connectivity, display explicit sync
  state, and avoid duplicate mutations through client-generated idempotency keys.

## Verification

The expected command set will evolve with the repository. Once scaffolded, the
minimum local gate is:

```text
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
docker compose config
```

Run commands from the paths documented in `docs/development-workflow.md`. Add
Playwright and container-backed integration tests when the changed behavior
requires them.

## Definition of Done

A change is complete only when production code, relevant tests, documentation,
OpenAPI changes, migrations, seed changes, and operational configuration are
consistent; builds and tests pass; no secrets or new warnings are introduced; and
remaining risks are stated in the handoff.
