# ADR 0016: Audit and governance controls

## Status

Accepted for Milestone 16.

## Decision

LoadIx records security-sensitive activity through one infrastructure-owned
`SecurityAuditWriter`. The writer captures tenant and actor context, a bounded
correlation and trace identity, safe request metadata, and optional before/after
summaries and reasons. Summaries are projected and redacted before persistence;
passwords, tokens, credentials, object-storage keys, and file bodies are not
valid audit payloads.

Audit rows are append-only in application tracking and at the database boundary:
the EF context rejects updates/deletes and the migration installs an immutable
SQL Server trigger. Audit consumers use deterministic ordering and tenant-scoped
queries.

Tenant governance is represented by explicit application use cases for retention
policy, personal-data inventory, bounded tenant export, deactivation-aware
anonymisation requests, and support-access logging. The export is a safe data
projection, not a database backup, and the anonymisation workflow does not
rewrite immutable audit history or delete document bodies.

## Consequences

- Sensitive workflow changes can be correlated and reviewed without copying
  request secrets or uploaded content into audit storage.
- Permission and security-stamp changes revoke existing sessions, so access
  changes take effect without waiting for token expiry.
- Retention and anonymisation policy remain explicit tenant operations and require
  operational/legal review before automated disposal is enabled.
- The SQL trigger is part of the reviewed migration and must be preserved in
  every production database. Break-glass database access requires separate
  controls and monitoring.

This decision provides security and governance foundations; it does not claim
that LoadIx is legally compliant.
