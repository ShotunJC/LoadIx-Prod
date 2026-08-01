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
returns the token once. The agent exchanges it for a device credential, which
is also returned once and stored only as a password-hash by the API. Rotation
revokes the previous credential before issuing a replacement; administrator
revocation disables the device and all of its credentials.

Every authenticated device request carries a device ID, device secret, current
Unix timestamp, nonce, and idempotency key. The API validates the timestamp
tolerance, verifies the hashed secret, resolves tenant and site from the stored
device, and records the request hash in `EdgeCommand`. Unique tenant/device
indexes on nonce and idempotency key make sequential retries replayable and
payload changes conflicts. Device endpoints are rate-limited separately from
browser endpoints. Reading and ticket submissions retain captured time and
received time independently so offline replay is supported without extending
credential validity.

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
  evidence for accepted device activity. Rejected requests are not persisted as
  readings or tickets and are surfaced through Problem Details/rate-limit
  responses.
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
encrypted store), not in logs or a world-readable configuration file. Pending
submissions should use an atomic, bounded local queue with crash recovery,
retention/backpressure rules, and clear offline/failed state. Heartbeats,
agent version, parser version, queue depth, and last successful submission
should be exposed through redacted diagnostics. Rotation and revocation should
be part of the site runbook before replacing or decommissioning hardware.

Before enabling a real indicator, record the exact model, firmware, electrical
interface, framing/encoding, stability semantics, unit semantics, and sample
captures in a reviewed protocol fixture. Compatibility should then be added as
an agent plugin and tested against those fixtures; it must not be inferred from
the generic simulator parser.
