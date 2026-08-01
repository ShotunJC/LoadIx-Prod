# ADR-045: Edge-device and weighbridge integration foundation

## Status

Accepted for the Milestone 15 foundation. Real indicator compatibility remains
unverified and is intentionally out of scope.

## Context

LoadIx needs a safe ingestion path for an on-site agent running close to a
weighbridge. The API must receive structured observations without becoming a
serial-driver catalogue or trusting tenant/site identifiers supplied by an
untrusted device. Installations may lose connectivity and must be able to retry
without creating duplicate readings.

## Decision

The API owns the tenant-bound device registry and the durable ingestion record.
The edge agent owns hardware access and protocol parsing. The foundation defines
these tenant-owned records:

- `EdgeDevice` and `EdgeDeviceCredential` for lifecycle, site binding, and
  per-device credentials;
- `WeighbridgeDevice` for the assigned indicator identity and parser contract;
- `RawWeightReading` and `StableWeightReading` for immutable observations;
- `EdgeCommand` for authenticated request evidence and replay/idempotency state;
  and
- `DeviceHeartbeat` for health history.

Registration is a two-step flow. An authorized tenant administrator creates a
short-lived, one-use bootstrap token. The API stores only its password-hash and
returns the token once. Before calling bootstrap, the agent generates the device
secret locally and durably records that secret, nonce, and idempotency key. The
API hashes the proposed secret and stores it with a replay command; it never
generates or persists the plaintext device secret. An exact retry after a lost
response returns an acknowledgement with no secret, while a changed secret or
payload returns `409 Conflict`.

Rotation is client-generated and two-phase. The current credential submits a
locally generated replacement. The old credential remains usable and the new
credential is marked `PendingConfirmation` until the agent authenticates with it
at `/api/v1/edge/credential/rotation-confirm`. Confirmation revokes the old
credential immediately. A pending replacement expires after the configured
overlap period; the old credential remains the recovery path. A request/admin
operation cleans up the stale replacement immediately, and a scheduled worker
also expires stale pending credentials. Only one pending replacement is
permitted. Administrator-triggered rotation uses the same lifecycle and
displays the client-generated replacement for delivery to the agent; it does not
revoke the old credential at request time.

Every authenticated device request carries a device ID, device secret, current
Unix timestamp, nonce, and idempotency key. The API validates the timestamp
tolerance, verifies the hashed secret, resolves tenant and site from the stored
device, and records the request hash in `EdgeCommand`. Unique tenant/device
indexes on nonce and idempotency key make sequential retries replayable and
payload changes conflicts. Command writes use duplicate-key recovery around the
same SaveChanges transaction as the resulting resource, so concurrent identical
requests reload the winning command rather than returning a database 500.
Unauthenticated/bootstrap traffic is IP rate-limited; authenticated requests use
`tenantId:deviceId` partitions so devices behind one NAT do not share a bucket.
Reading and ticket submissions retain captured time and received time
independently so offline replay is supported without extending credential
validity.

The reading contract preserves the displayed decimal value, unit, stable flag,
captured/received timestamps, device and weighbridge IDs, optional vehicle
registration, serial-message metadata, parser version, and correlation ID. A
parser-plugin interface is defined in the application layer. Milestone 15 ships
only a simulator and a deliberately generic `WEIGHT|key=value` line parser for
tests; it does not claim compatibility with any real indicator.

## Consequences

- Controllers can bootstrap, monitor, rotate, revoke, and diagnose devices from
  the web UI without seeing stored secrets again.
- Device submissions remain tenant/site-bound even if a request is replayed or
  contains an ID from another tenant.
- The API remains independent of serial ports and manufacturer protocol details.
- `EdgeCommand` and the security audit table provide operational and security
  evidence for accepted device activity. Authentication failures, stale
  timestamps, malformed envelopes, replay mismatches, binding failures, and
  failed bootstrap/rotation attempts are recorded with deterministic failure
  codes, trace/correlation data, remote IP, and hashes of nonce/idempotency
  values. Audit writes are best effort during authentication and never block the
  authentication decision; rate-limit rejection is also emitted as a structured
  security log because the rejection callback may not have a safe tenant DB
  context.
- Composite restrictive foreign keys cover tenant, site, device, credential,
  weighbridge, command, raw-reading, stable-reading, heartbeat, and ticket
  relationships. The one-stable-reading-per-raw-reading and replay uniqueness
  constraints remain database-enforced.
- The simulator's local state file is a test artifact. It must not be used as a
  production secret store.

## Later Raspberry Pi or industrial-PC deployment

The production agent should be packaged as a versioned .NET worker and run on a
supported 64-bit Linux image using `systemd` (or an approved container runtime)
under a dedicated non-root account. Serial access should use stable
`/dev/serial/by-id` paths and narrowly scoped udev rules; the API must never be
given direct serial access. Protocol adapters should be separate plugin
assemblies or processes with a versioned parser contract and fixture tests
derived from verified manufacturer documentation.

The host should use outbound-only HTTPS to the LoadIx API, strict certificate
validation, an accurate NTP-synchronised clock, and firewall rules that deny
unneeded inbound access. Device secrets should be held in the platform's
protected secret store (for example, an OS keyring or deployment-managed
encrypted store), not in logs or a world-readable configuration file. The
simulator JSON state is intentionally a test-only artifact and is not a model
for production storage. Pending submissions should use an atomic, bounded local queue with crash recovery,
retention/backpressure rules, and clear offline/failed state. Heartbeats,
agent version, parser version, queue depth, and last successful submission
should be exposed through redacted diagnostics. Rotation and revocation should
be part of the site runbook before replacing or decommissioning hardware.

Before enabling a real indicator, record the exact model, firmware, electrical
interface, framing/encoding, stability semantics, unit semantics, and sample
captures in a reviewed protocol fixture. Compatibility should then be added as
an agent plugin and tested against those fixtures; it must not be inferred from
the generic simulator parser.
