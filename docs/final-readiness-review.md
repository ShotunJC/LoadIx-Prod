# LoadIx final production-readiness review

Review date: 2026-08-08  
Scope: complete repository at the working-tree state reviewed on this date  
Reviewer stance: adversarial; passing tests were treated as evidence, not proof

## Readiness recommendation

**Do not deploy LoadIx to production or use it to produce customer invoices.**

A tightly controlled, single-tenant-at-a-time operational pilot can proceed only
after uploads and invoice preparation are disabled, one worker replica is enforced,
synthetic/non-sensitive documents are used, and the named pilot gates below are
accepted by accountable product, finance, security, and operations owners. The
repository has a strong security foundation and its final executable test suite is
green, but important business controls are represented by booleans or deployment
instructions rather than enforceable behavior.

No Critical finding was identified. Seven High defects were safely fixed during
this review. Five unresolved High finding groups remain deployment blockers for
their affected capabilities.

## Severity summary

| Severity | Fixed | Unresolved | Meaning for release |
| --- | ---: | ---: | --- |
| Critical | 0 | 0 | None identified. |
| High | 7 | 5 | Blocks production; affected pilot features must be disabled. |
| Medium | 0 | 12 | Requires tracked owners and pilot constraints. |
| Low | 0 | 4 | Hygiene and maintainability work. |

## Fixed findings

| ID | Severity | Finding and impact | Remediation and regression evidence |
| --- | --- | --- | --- |
| FIX-01 | High | Completed, cancelled, and rejected trips still accepted source/destination weighbridge edits. Final operational and financial evidence could be changed after finalisation. | `Trip` now rejects both mutations in terminal states. `TripStateMachineTests.FinalTripRejectsWeighbridgeChangesWithoutMutatingWeights` verifies all terminal states and no partial mutation. |
| FIX-02 | High | Finance always preferred destination weight, ignored the snapshotted rate type, and treated kilograms as tonnes. Source-net contracts and kilogram tenants could be materially over/under billed. | `Trip.ResolveBillableTonnes` selects source or destination by rate type, treats fixed trips as zero tonnes, fails closed on missing required weight, and converts kg to tonnes. `TripFinancialsTests.ResolvesSourceRateWeightInTonnesFromKilograms` covers rate selection and conversion. Reconciled-rate policy remains FIND-01. |
| FIX-03 | High | Contract/rate/resource effective dates, invoice batch dates, and “completed today” operations queries used UTC calendar dates. Around midnight UTC, South African tenants could receive the wrong rate/status/day result. | Added tenant-time-zone-aware `TenantClock`, used by trip generation, instruction, contract, finance, and operations paths. `TenantClockTests` covers the Johannesburg date boundary. Residual master-data/report date issues are FIND-09. |
| FIX-04 | High | Edge rate limiting ran before edge-scheme authentication and tenant resolution, so the intended tenant/device partition was never trusted. Moving it naively caused valid edge requests to receive 403. | Edge requests now receive a raw IP quota before expensive authentication and a second trusted tenant/device quota after edge authentication, before tenant context and authorization. Architecture tests enforce ordering/partition behavior; all 57 integration tests, including edge replay/rotation/concurrency tests, pass. |
| FIX-05 | High | The unauthenticated edge partition included the client-supplied device GUID. An attacker could rotate GUIDs and evade the credential-verification limit. | The pre-authentication partition is now IP-only; the device identifier is used only after successful authentication. `UnauthenticatedPartitionDoesNotTrustTheDeviceHeader` is the regression guard. |
| FIX-06 | High | An ambiguous mobile network failure queued a retry with a new idempotency key. If the server committed before the response was lost, replay could create a duplicate command. | The driver creates one key before the first attempt and preserves it for offline/transient replay. `command-queue.test.ts` verifies caller-provided key preservation. |
| FIX-07 | High | The IndexedDB driver queue was global and survived failed refresh/login changes. A later user on the same browser could see or replay the prior driver's trip IDs, notes, and locations. | Failed refresh, successful login, and logout now clear driver offline data. `login-flow.test.tsx` covers failed startup refresh followed by login. |

## Unresolved findings

### High

| ID | Area | Finding, evidence, and required disposition |
| --- | --- | --- |
| FIND-01 | Domain, trip state, weights, finance | Reconciliation is not implemented as a calculation or versioned decision. `Trip.ConfirmReconciliation()` only sets `IsReconciled`; `ConfirmRequiredDocuments()` only sets a boolean and does not query document metadata. Reconciled/base-plus rates currently use destination net weight because no reconciled quantity exists. The tolerance, shortage/excess policy, accepted unit, document types, override authority, and immutable input snapshot must be specified and implemented before completion can unlock finance. **Production and invoice-preparation blocker.** |
| FIND-02 | Authorization, privilege escalation, finance | One `FinanceManage` permission can configure tax, calculate, approve trip financials, create/approve/export batches, and the same actor is not prevented from approving their own work. Four-eyes policy is listed as undecided in the architecture. Define incompatible roles, maker/checker rules, emergency override, and tests before real invoicing. **Production finance blocker.** |
| FIND-03 | File upload | No production malware-scanner adapter exists. Every non-development scanner name resolves to `UnconfiguredMalwareScanner`; startup accepts a non-development name but uploads then fail closed. This is safe failure behavior, not a usable production implementation. Select and implement a maintained scanner, test malware/timeout/unavailable cases, and monitor quarantine. **Upload blocker.** |
| FIND-04 | Object storage, backup | Application authorization and opaque tenant keys are sound, but bucket privacy, least-privilege policy, encryption, versioning/retention, replication, and lifecycle rules exist only in prose. The adapter can create a bucket but does not enforce those controls. No restore rehearsal evidence exists. Provision reviewed infrastructure and complete a measured SQL/object/key-ring restore before sensitive or irreplaceable pilot data. **Production blocker; pilot limited to recoverable non-sensitive evidence.** |
| FIND-05 | Message loss, outbox, SignalR, notifications | SQL rows are durable, but operational and notification dispatchers use read/send/save without a claim/lease. Two workers can send the same row, and a crash after provider send but before save duplicates delivery. SignalR rows are marked dispatched even when no client is connected, so they are invalidations rather than durable events. SMS is always the local logging sender. Keep exactly one worker, do not claim guaranteed SMS/event delivery, then add leases, provider message IDs/idempotency, client dedupe, reconciliation, and a real SMS adapter before scaling. **Multi-worker/notification blocker.** |

### Medium

| ID | Area | Finding and recommendation |
| --- | --- | --- |
| FIND-06 | Authentication/user administration | Invitation acceptance and role assignment span Identity and domain updates without one transaction. A mid-flow failure can leave a partially provisioned user or partially changed roles. Add a transactional/saga-style use case and failure-injection tests. |
| FIND-07 | Domain correctness | `AddOperationalNote` assigns the enlarged string before checking the 4,000-character aggregate limit. A failed command leaves the tracked aggregate invalid until the request scope is discarded. Validate the candidate before assignment and test no mutation on failure. |
| FIND-08 | Deployment/migrations | Pilot deployment applies migrations before switching application containers. No enforced expand/contract compatibility gate proves that the old application remains valid during migration or rollback. Require reviewed idempotent SQL, backward-compatible migration policy, and a rehearsal for every release. |
| FIND-09 | Time zones | `MasterDataService` still evaluates compliance using UTC date; legacy report-job UTC-to-`DateOnly` conversion can shift user-selected local dates. Invalid reporting time zones can fall back to UTC. Use `TenantClock` consistently and fail configuration validation early. |
| FIND-10 | SQL/performance | Tenant filters and relevant indexes are broad and pagination is bounded, but no current pilot-shaped execution-plan run was performed. Operations endpoints can return up to 5,000 joined rows/locations, and report artifacts are `varbinary(max)`. Capture plans/IO under target volume and reduce/stream high-cardinality responses. |
| FIND-11 | Offline mobile | Queue-at-rest data is not encrypted and remains browser-profile accessible until auth boundary cleanup. Sync has no per-user/tenant namespace, conflict review screen, background-sync durability guarantee, or device-management remote wipe. Restrict shared devices and complete multi-user/offline chaos tests. |
| FIND-12 | Accessibility | Semantic labels, visible focus styles, and responsive primitives are present, but there is no automated axe gate or documented screen-reader/keyboard audit. Custom modal dialogs do not have demonstrated focus trapping/return coverage. Perform WCAG 2.2 AA manual and automated testing before broad use. |
| FIND-13 | Responsive layout | The driver mobile scenario passes, but administrative/finance pages have no small-viewport E2E matrix. Tables rely on horizontal scrolling. Test 320/360/768 px, zoom/reflow, touch targets, landscape, and slow devices. |
| FIND-14 | Frontend delivery | The production entry chunk is 1,585.46 kB minified (433.19 kB gzip), above Vite's 500 kB warning. This is material for intermittent mobile links. Split maps/admin/reporting dependencies and set a bundle budget. |
| FIND-15 | Verification hygiene | `dotnet format --verify-no-changes` fails across the checkout because files are CRLF while `.editorconfig` requires LF. This obscures real formatting regressions. Normalize line endings in a dedicated reviewed change and enforce `.gitattributes`/clean checkout behavior. |
| FIND-16 | Documentation accuracy | `security-review.md` records older tool/test counts and Docker Scout evidence, and backup/deployment text calls “the SQL outbox” authoritative for RabbitMQ although the operational outbox only drives SignalR and report export has a separate broker workflow. Refresh claims from executable evidence and distinguish invalidation, notification, and integration delivery. |
| FIND-17 | Testing | Strong tenant/auth/edge/integration coverage exists, but missing focused tests include maker/checker finance, real reconciliation/tolerance, required-document derivation, scanner/provider contract tests, outbox crash/lease behavior, restore drills, query plans, automated accessibility, and admin mobile layouts. |

### Low

| ID | Area | Finding and recommendation |
| --- | --- | --- |
| FIND-18 | Secrets scan | Gitleaks reports five occurrences of the same documented development signing key in current/history files. The value is explicitly development-only, but the unallowlisted result makes the release scan red. Replace it with an unmistakable non-secret fixture or add a narrowly fingerprinted, reviewed allowlist with comments. |
| FIND-19 | Tooling | Local `dotnet-ef` is 10.0.3 while runtime/packages are 10.0.10. The idempotent script generated successfully; align the manifest to remove version drift. |
| FIND-20 | Architecture | Operations endpoint handlers contain sizeable EF queries and day-boundary policy directly in the API project, contrary to the documented application-layer boundary. Move them behind application queries. |
| FIND-21 | Logging/observability | Source review found bounded/redacted security logging and no direct token/file-content logging, but production sink configuration, proxy query-string redaction, retention, and SIEM alerts are external and unverified. Validate them during deployment rehearsal. |

## Review-area conclusions

- Tenant isolation: global filters cover tenant-owned operational sets; sampled
  bypasses use explicit tenant predicates. Cross-tenant endpoint tests pass.
- Authorization/authentication/refresh: short-lived JWTs are checked against
  active user, tenant membership, security stamp, and refresh session. Refresh
  tokens are random, hash-stored, cookie-protected in production, rotated in a
  serializable transaction, and family-revoked on reuse. No direct auth bypass was
  found; FIND-02 and FIND-06 remain.
- Upload/object access: size/count, extension, MIME, signature, image geometry,
  quarantine, authorized download, and tenant-scoped metadata checks are present.
  FIND-03/FIND-04 prevent production enablement.
- Decimal handling: backend money and measured quantities use `decimal` with
  explicit SQL precision and rounding. FIX-02 corrected the material unit/rate
  defect. Reconciliation remains undefined.
- Concurrency/idempotency: row versions and idempotency records cover major trip,
  instruction, document, mobile, and edge commands. FIX-06 closes ambiguous mobile
  replay. Messaging and user-admin gaps remain.
- SignalR: groups derive from validated tenant/user claims and the hub does not
  accept tenant identity from the client. No cross-tenant subscription path was
  found. Delivery durability is limited as described in FIND-05.
- Edge replay: timestamp, nonce, credential state, and idempotency constraints are
  present and integration-tested. FIX-04/FIX-05 correct throttling boundaries.
- Sensitive logging/secrets: no production credential was identified in tracked
  content. Development fixtures trigger FIND-18.

## Deployment blockers

1. Implement and approve reconciliation, weight-unit, tolerance, required-document,
   and override policy (FIND-01).
2. Implement finance maker/checker segregation and prohibit self-approval (FIND-02).
3. Supply and test a real production malware scanner or keep uploads disabled
   (FIND-03).
4. Provision and verify private encrypted/versioned object storage and rehearse
   complete restore including SQL, objects, metadata, and Data Protection keys
   (FIND-04).
5. Enforce one worker replica and disable external notification claims until
   FIND-05 is fixed.
6. Establish an expand/contract migration gate and rehearse rollout/rollback.
7. Resolve or formally disposition the failing repository line-ending gate and
   Gitleaks development-fixture findings so release automation has an unambiguous
   signal.

## Permitted pilot limitations

If accountable owners approve a pre-production pilot, constrain it to one host,
one API replica, one worker replica, a small named user cohort, and synthetic or
recoverable operational data. Disable uploads, SMS, customer invoice export, and
automated financial approval. Do not represent SignalR as durable delivery. Do not
use shared driver browser profiles. Keep manual dispatch/reconciliation records
outside LoadIx, cap operations-board use, monitor SQL/outbox/notification lag, and
retain a tested manual fallback.

## Manual test checklist

- [ ] Attempt every trip transition from every state, including stale row versions,
      cancellation/rejection, and post-terminal weight edits.
- [ ] Reconcile source/destination shortages, excesses, missing weights, kg/tonne
      tenants, each rate type, minimum charges, surcharge, VAT treatment, and exact
      rounding against finance-approved examples.
- [ ] Verify two tenants cannot read/write guessed IDs for every aggregate,
      document object, report, SignalR connection, notification, edge device, or
      support-context request.
- [ ] Exercise self-approval, delegated-role escalation, invitation interruption,
      user deactivation, password/security-stamp change, refresh reuse, parallel
      refresh, expired cookies, and support access expiry/reason logging.
- [ ] Upload EICAR and malformed/polyglot/oversized/multi-frame/image-bomb files;
      interrupt scanner/object storage; verify quarantine cleanup and authorized
      download only.
- [ ] Replay edge requests with changed nonce/key/body/timestamp/device ID, rotate
      credentials concurrently, revoke during offline replay, and verify IP plus
      tenant/device throttles.
- [ ] Lose the first mobile response after server commit; replay offline as the
      same user, then switch users on one browser and inspect IndexedDB before/after.
- [ ] Crash each dispatcher before send, after provider send, and before SQL save;
      run two workers to prove claim/idempotency behavior before scaling.
- [ ] Connect concurrent SignalR clients from two tenants and a revoked session;
      inspect proxy logs to ensure access-token query strings are absent/redacted.
- [ ] Capture SQL actual plans and IO/time for board, locations, trip lists,
      finance, report filters, outbox, and notification queues using pilot-shaped
      data; verify deterministic page boundaries.
- [ ] Keyboard/screen-reader test all navigation, forms, errors, dialogs, tables,
      maps, upload progress, focus return, 200%/400% zoom, and high contrast.
- [ ] Test 320, 360, 768, and desktop widths, landscape, touch-only operation,
      offline/slow links, and the production bundle on target driver hardware.
- [ ] Rehearse backup/restore and point-in-time recovery into an isolated target;
      reconcile SQL rows with object versions and verify existing protected data.
- [ ] Rehearse migration failure, application rollback, dependency outage,
      certificate expiry, disk full, secret rotation, and incident escalation.

## Exact verification and scan commands run

Commands were run from the repository root unless a working directory is noted.
PowerShell's script policy blocks `npm.ps1`, so `npm.cmd` was used.

```text
dotnet --version
node --version
docker version
dotnet restore LoadIx.slnx
npm.cmd ci --prefix apps/loadix-web
dotnet format LoadIx.slnx --verify-no-changes --no-restore
dotnet build LoadIx.slnx --configuration Release --no-restore -warnaserror
dotnet test LoadIx.slnx --configuration Release --no-build
npm.cmd run format:check --prefix apps/loadix-web
npm.cmd run lint --prefix apps/loadix-web
npm.cmd run typecheck --prefix apps/loadix-web
npm.cmd run test --prefix apps/loadix-web -- --run
npm.cmd run build --prefix apps/loadix-web
npm.cmd run e2e --prefix apps/loadix-web
npm.cmd audit --prefix apps/loadix-web --audit-level=high
dotnet list LoadIx.slnx package --vulnerable --include-transitive
dotnet ef migrations script --idempotent --project src/LoadIx.Infrastructure --startup-project src/LoadIx.Api --configuration Release --no-build --output NUL
docker compose config --quiet
docker compose -f compose.pilot.yaml config --quiet
docker compose up --build --detach --wait
docker build --file deploy/api.Dockerfile --tag loadix-api:security .
docker build --file deploy/worker.Dockerfile --tag loadix-worker:security .
docker build --file deploy/web.Dockerfile --tag loadix-web:security .
docker build --file deploy/migrations.Dockerfile --tag loadix-migrations:security .
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v loadix-trivy-worker-cache:/root/.cache/ aquasec/trivy:0.69.3 image --scanners vuln,secret --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 --skip-version-check <image>
docker run --rm -v "${PWD}:/repo" zricethezav/gitleaks:v8.28.0 detect --source=/repo --log-opts=--all --redact --verbose --no-banner --exit-code=1
```

Additional focused .NET tests were run while correcting edge middleware ordering;
the first complete rerun exposed eight 403 failures, the focused replay test then
passed, and the final complete suite below passed. Trivy filesystem scanning also
ran with vulnerability, secret, misconfiguration, and license analyzers. Parallel
image scans caused a shared-cache lock/segmentation failure; both affected images
were rerun sequentially and passed. One build invoked concurrently with the final
integration run failed on a Windows testhost file lock; the same build and full
suite then passed sequentially. Manual inspection used `rg`, `git diff`, and
targeted file reads across domain, API, infrastructure, tests, frontend, deploy,
workflow, and documentation paths.

## Final build, test, and scan results

| Gate | Final result |
| --- | --- |
| Release build, warnings as errors | Pass: 0 warnings, 0 errors. |
| .NET unit tests | Pass: 69. |
| Architecture tests | Pass: 7 after final edge partition guard. |
| Integration tests | Pass: 57. |
| Frontend lint/typecheck | Pass. |
| Frontend unit/component tests | Pass: 12 files, 19 tests. |
| Playwright | Pass: 9 tests against the live Compose API; the first run without an API failed as expected with connection refusal. |
| Frontend production build | Pass with 1,585.46 kB entry-chunk warning. |
| Frontend format | Pass after narrow Prettier cleanup. |
| .NET format | **Fail:** repository-wide CRLF versus required LF. |
| NuGet vulnerability audit | Pass: no vulnerable packages reported. |
| npm audit | Pass: 0 vulnerabilities. |
| Trivy repository filesystem | Pass: no reported dependency vulnerability, secret, or Dockerfile misconfiguration finding in the completed scan. |
| Trivy final images | Pass: 0 fixable High/Critical vulnerabilities or secrets in API, worker, web, and migrations images. |
| Gitleaks full history | **Fail:** 5 development-key fixture findings; no production credential identified. |
| EF idempotent migration script | Pass, with local tool/runtime version warning. |
| Development Compose config/build/health | Pass; SQL Server, Redis, RabbitMQ, MinIO, Mailpit, API, and web healthy. |
| Pilot Compose config | Pass with non-secret placeholder environment values. |
| Backup/restore, failure drills, production provider tests | Not run; external infrastructure and approved providers were unavailable. |

## Recommended next milestones

1. **Reconciliation and evidence milestone:** model measured/reconciled quantities,
   units, tolerance, shortage/excess, mandatory document derivation, overrides,
   audit snapshots, and end-to-end finance examples.
2. **Financial control milestone:** maker/checker roles, self-approval prevention,
   invoice-number policy, corrections/credit handling, accounting export contract,
   and finance sign-off.
3. **Durable delivery milestone:** outbox/notification leases, provider idempotency,
   real SMS/scanner adapters, dead-letter operations, client dedupe, and chaos tests.
4. **Production data-protection milestone:** object-store IaC/policies, secret manager,
   retention/legal hold, encrypted backups, measured restore, and incident rehearsal.
5. **Pilot performance and UX milestone:** current execution plans/load test, bundle
   splitting, accessibility audit, responsive matrix, offline multi-user conflicts,
   and target-device testing.
6. **Release integrity milestone:** normalize line endings, make Gitleaks fixtures
   explicit, align EF tooling, refresh documentation evidence, and enforce all gates
   on clean Linux and Windows checkouts.
