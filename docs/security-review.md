# LoadIx security review

## Review scope

This review records the Milestone 16 audit, security-hardening, and data-governance
work completed for the LoadIx pilot baseline. It covers the API, application and
infrastructure layers, persistence, edge-device boundary, deployment overlays,
tests, and dependency/container scans.

This is a technical security review. It does **not** claim that LoadIx is legally
compliant, certified, or ready to satisfy a particular privacy or sector
regulation. Legal basis, records schedules, data-subject procedures, contracts,
and operational controls require review by the appropriate owners.

## Executive result

Milestone 16 adds a central safe audit-writer, append-only audit persistence,
session and authorization hardening, tenant-scoped governance foundations,
defensive HTTP and upload controls, edge-device throttling, and security
regression coverage.

The application and integration test suites pass, package scans reported no
known high-severity dependency findings, and the locally built API image has no
known critical or high findings in Docker Scout. Production rollout still
requires the recommendations in this document, especially secret/scanner
configuration, distributed rate limiting, retention operations, and review of
the remaining base-image findings.

## Findings and remediation

| Area | Finding | Remediation delivered | Remaining risk or boundary |
| --- | --- | --- | --- |
| Auditability | Sensitive workflows did not have one consistent, safe audit boundary. | `SecurityAuditWriter` records tenant, actor, action, entity, timestamp, correlation/trace identity, safe request metadata, bounded summaries, and reasons. It covers authentication, role/permission changes, support context, master data, trip transitions, weights, reconciliation overrides, document access, financial approvals, exports, and edge credential changes. | Audit review, alerting, and retention operations still need an owning production process. |
| Audit integrity | Application code could otherwise update or delete audit rows. | EF tracking rejects updates/deletes and the M16 SQL migration adds an immutable SQL Server trigger. Audit payloads are redacted and bounded; passwords, tokens, credentials, object-storage keys, and file bodies are excluded. | A database principal with unrestricted break-glass rights can bypass application controls. Restrict, monitor, and periodically review such access. |
| Authentication | Access remained valid after identity/session state changed, and refresh replay needed a transactional boundary. | JWT validation checks active user state, email confirmation, tenant, security stamp, and refresh-session state. Refresh rotation/reuse detection runs in a serializable transaction; password changes, deactivation, role changes, and permission changes revoke sessions. Login responses remain non-enumerating and account lockout is enforced. | MFA/federation and a time-bound support-grant workflow remain deployment/product decisions. |
| Authorization and tenant isolation | Object identifiers and role data are untrusted input. | Tenant query filters are retained and use cases apply explicit tenant/permission checks. Role queries are tenant-scoped. Support access requires a platform-admin claim, an active target tenant, and a bounded reason. Governance export/anonymisation operations are permissioned and tenant-scoped. | Tenant isolation must continue to be tested whenever a new aggregate or endpoint is added. |
| CORS and browser policy | Broad origins or permissive browser execution would increase exposure. | CORS allows only the configured web origin and explicit headers/methods. API responses receive CSP (`default-src 'none'`), frame, MIME-sniffing, referrer, permissions, cross-origin, and production HSTS headers. `AllowedHosts` is no longer `*`. | The trusted proxy IP must be configured correctly; a bad proxy/network configuration can invalidate HTTPS and client-IP assumptions. |
| Rate limiting | Credential and API abuse could consume application resources. | Global API limits, named authentication/password/document/user-admin limits, and an edge-device limiter are enabled. Failed edge credential requests are throttled using a bounded device/IP partition, and 429 responses include `Retry-After`. | The current limiter is process-local and fixed-window. Multi-instance production should add a distributed limiter and/or WAF controls. |
| Input and file handling | Uploaded data and untrusted command fields are common attack surfaces. | Request validation, bounded pages/text, explicit reason limits, idempotency, optimistic concurrency, multipart limits, extension/type/signature checks, quarantine, fail-closed malware-scanner configuration, opaque storage keys, and authorized streaming are enforced. | A real maintained malware scanner and object-store lifecycle policy must be supplied in production. `Unconfigured` intentionally blocks uploads outside development. |
| SQL and object references | Unsafe SQL and direct object references can bypass tenant boundaries. | Application queries use EF Core projections and tenant-scoped predicates; new migration SQL is limited to reviewed DDL for the immutability trigger. IDs are resolved through authorized application services, not trusted from the client. | Continue review of any future raw SQL and database roles. |
| Mass assignment | Client fields must not set tenant or security authority. | Endpoint request models are explicit. The invitation flow ignores client-supplied tenant/authority fields, and role/permission changes validate the actor's authority and revoke affected sessions. | Keep request models separate from persistence entities as new endpoints are added. |
| SignalR | Real-time subscriptions must not cross tenant or permission boundaries. | The trip hub requires authorization, resolves tenant/user claims from the authenticated connection, and only joins validated tenant/user groups. Query-string access tokens are accepted only for the browser SignalR transport limitation; request metadata excludes query strings. | Use HTTPS and short-lived/revocable tokens, and ensure proxy/access-log configuration never records query strings. |
| Edge devices | Hardware-facing endpoints are exposed to replay, credential theft, and noisy failure traffic. | Device credentials are hashed, rotation has overlap-and-confirm semantics, revocation is checked, timestamps/nonces/idempotency protect replay and duplicates, failure events are audited with hashed request identifiers, and edge endpoints are rate-limited. | Device secret storage, certificate identity, network segmentation, and hardware installation controls remain deployment decisions. |
| Errors and logging | Error output and logs can accidentally disclose secrets or personal/file content. | RFC Problem Details use generic client messages and trace IDs. Operational/audit logging records exception types or bounded identifiers rather than exception messages, credentials, tokens, or file bodies. Audit-summary unit tests verify redaction. | Central log sinks still need retention, access control, SIEM routing, and production-specific redaction/alert rules. |
| Secrets and deployment | Development defaults must not become public deployment credentials. | Production startup validates signing keys and malware-scanner configuration; the public demo overlay selects `Production`, supports environment-injected secrets, and configures trusted proxy input. The API image runs as a non-root application user. | Use a managed secret store, rotate credentials, configure SMTP/DB/object-store/RabbitMQ secrets, and do not expose the local compose defaults publicly. |
| Data governance | Retention, inventory, export, deactivation, and anonymisation need explicit foundations. | Added tenant retention-policy records with legal-hold reason support, a personal-data inventory endpoint, bounded safe tenant export, deactivation-aware anonymisation requests/approval, and support-access audit logging. Exports exclude passwords, access/refresh tokens, device credentials, storage keys, and document bodies. | No automated deletion scheduler or complete object-store/backup purge is included. Anonymisation intentionally does not rewrite immutable audit history or document bodies; legal and operational review is required before disposal. |

## Audit event contract

Audit rows are tenant-aware where the action has a tenant context and contain the
actor, action, entity type and identifier, UTC timestamp, correlation ID, and
safe request metadata. Before/after summaries are projections rather than entity
serialization. Reasons are required by the affected workflow for overrides,
corrections, support access, and other policy-sensitive changes.

The writer strips sensitive keys and values, bounds text/JSON lengths, omits query
strings, and never accepts passwords, bearer tokens, refresh tokens, secrets,
private keys, object-storage keys, or uploaded file content as audit data.

## Security test coverage

The regression suite now exercises the following controls:

| Required scenario | Coverage |
| --- | --- |
| Cross-tenant access | `TenantPersistenceTests`, `MasterDataEndpointTests`, `ContractEndpointTests`, `LoadInstructionEndpointTests`, `TripEndpointTests`, `FinanceEndpointTests`, `DocumentEndpointTests`, and `EdgeEndpointTests` attempt reads/writes with another tenant's IDs. |
| Privilege escalation and support access | `AuthenticationEndpointTests.InvitationAssignsRoleAndPolicyRejectsGreaterAuthority`, `NormalTenantUserCannotEnterAnotherTenantContext`, and `PlatformAdministratorSupportContextIsVisibleAndAudited`. Role/permission application checks are also covered by the user-admin route and policy tests. |
| Object-ID tampering | Document, trip, contract, master-data, finance, load-instruction, and edge tests use guessed or foreign identifiers and assert denial or not-found behavior. |
| Expired/revoked authentication | `AuthenticationEndpointTests.ExpiredAccessTokenIsRejectedAndAudited`, revoked refresh-session reuse, deactivated-user access-token rejection, security-stamp/session invalidation, and token-validation failure paths are covered by authentication and edge tests. |
| Malicious uploads | `DocumentEndpointTests.RejectsInvalidSignaturesExcessiveFilesAndKeyReuse` covers invalid signatures, excessive files, and unsafe key reuse; successful uploads are streamed through the quarantine/scanner boundary. |
| Replay and duplicate submissions | Authentication refresh reuse, edge nonce/idempotency/replay tests, concurrent document uploads, trip/load-instruction idempotency, and concurrent edge submissions are covered. |
| Mass assignment | `AuthenticationEndpointTests.InvitationAssignsRoleAndPolicyRejectsGreaterAuthority` supplies client-controlled authority/tenant fields and verifies they cannot broaden access. |
| Rate limits | `EdgeEndpointTests.InvalidEdgeCredentialRequestsAreRateLimited` sends repeated failed device requests and verifies RFC Problem Details plus `Retry-After`. Named and global limiters are also wired through the API route configuration. |
| Sensitive log/audit redaction | `AuditSummaryTests` verifies passwords, access tokens, storage keys, authorization values, and file bodies are removed or redacted while safe fields remain. |

## Verification evidence

Executed during this review:

- `dotnet build --no-restore --configuration Release --warnaserror` - passed;
- `dotnet test tests/LoadIx.UnitTests/LoadIx.UnitTests.csproj --no-build --configuration Release` - 58 passed;
- architecture tests - 5 passed;
- `dotnet test tests/LoadIx.IntegrationTests/LoadIx.IntegrationTests.csproj --no-build --configuration Release` - 55 passed;
- `npm.cmd ci`, lint, typecheck, test, and build - passed; npm reported 0 vulnerabilities;
- `dotnet list LoadIx.slnx package --vulnerable --include-transitive` - no vulnerable packages reported;
- Docker Scout scan of the refreshed `loadix-api:security` image - 0 critical, 0 high, 3 medium, and 5 low findings; remaining findings are base-image/OS packages without a currently available fix in the scanned image;
- `docker compose config` and the base/demo compose configuration - passed;
- EF migration listing - the M16 migration is latest and the Testcontainers integration run applied it successfully.

The repository's existing format gates are not clean: `dotnet format
--verify-no-changes` reports broad pre-existing line-ending changes, and the
frontend format check reports eight existing files. Those failures were not
auto-normalized because doing so would create unrelated repository-wide changes;
they should be cleaned up in a separate formatting change.

## Remaining risks

1. Governance is a foundation, not a complete privacy/records-management
   program. Retention schedules, legal holds, export fulfillment, deletion
   approvals, anonymisation scope, backup expiry, and object-store lifecycle
   behavior need named owners and documented procedures.
2. The image scan has no critical/high findings after the runtime refresh, but it
   still has medium/low OS-package findings. Keep the scan as a release gate and
   refresh the SDK/runtime/OS layers regularly.
3. The malware scanner is intentionally fail-closed but not supplied by this
   repository. Uploads must remain disabled until a maintained scanner is
   configured and its failure behavior is tested in the deployment environment.
4. In-process rate limiting does not coordinate across replicas or regions. Add
   a distributed limiter or edge/WAF policy before horizontal scaling.
5. Immutable audit history is not a substitute for database least privilege.
   Protect migration and break-glass credentials and monitor privileged SQL
   access.
6. SignalR browser-token handling and support access are sensitive operational
   surfaces. Enforce HTTPS, redact proxy logs, use short session lifetimes, and
   prefer just-in-time, time-bound support grants for production operations.

## Production recommendations

Before pilot traffic is admitted:

1. Deploy the public API with `ASPNETCORE_ENVIRONMENT=Production`, a unique
   signing key from a secret manager, non-default dependency credentials, a
   configured `TrustedProxyIp`, explicit allowed hosts/origins, HTTPS, and a
   real malware scanner. Do not publish the development compose defaults.
2. Apply and review the M16 migration in every environment; verify the audit
   trigger exists and that the application database identity cannot update or
   delete audit rows.
3. Configure centralized logs and SIEM alerts for token rejection, refresh
   reuse, lockouts, support access, permission changes, audit-write failures,
   upload rejections, edge credential failures, and rate-limit responses.
4. Establish a scheduled, reviewed retention process that considers audit rows,
   operational records, document metadata, object bodies, backups, and legal
   holds. Test export, deactivation, anonymisation, restore, and incident
   response with synthetic tenant data.
5. Run dependency and container scans on every pull request and release; fail
   releases on high/critical findings, track accepted exceptions, and refresh
   the remaining OS-package findings.
6. Put distributed throttling/WAF controls, network segmentation, database
   firewalling, object-store policy, secret rotation, backup encryption, and
   restore monitoring around the deployment.
7. Obtain independent security, privacy, finance, and operational sign-off for
   pilot scope. Record any accepted risk and its expiry/owner rather than
   treating this document as a compliance attestation.
