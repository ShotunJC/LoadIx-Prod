# Pilot incident response

This procedure covers availability, security, privacy, data-integrity, and
dedicated-server incidents. Replace placeholders in the protected operations
system before go-live; do not commit personal phone numbers or customer data.

## Severity

| Severity | Example                                                                                            | Initial target                                                               |
| -------- | -------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| P1       | Cross-tenant exposure, active credential compromise, unrecoverable data loss, or pilot-wide outage | Page technical, security, and privacy owners immediately; continuous updates |
| P2       | Major workflow unavailable, repeated failed processing, or material tenant impact                  | Engage on-call promptly; regular updates                                     |
| P3       | Degraded feature with workaround or isolated tenant issue                                          | Triage in support queue and schedule owner                                   |
| P4       | Question, cosmetic defect, or documentation issue                                                  | Normal support response                                                      |

Targets are proposals until the pilot agreement defines binding service levels.

## Contacts and roles

Maintain these roles and escalation paths in the protected incident system:

- incident commander: [owner/contact];
- dedicated-server technical on-call: [owner/contact];
- database/storage owner: [owner/contact];
- RabbitMQ/SMTP/SMS/maps/scanner vendors: [contacts];
- security/privacy lead: [owner/contact];
- pilot/customer owner: [owner/contact];
- legal/POPIA contact: [owner/contact].

## First response

1. Open an incident record with UTC start time, detector, severity, affected
   tenants/workflows, and incident commander. Preserve alerts and trace IDs.
2. Contain safely: stop a rollout, run the protected app rollback, restrict
   Caddy/SSH ingress, disable an affected upload/command path, isolate a tenant,
   or place the system in maintenance mode. Do not delete evidence.
3. Decide whether this is a security/privacy event. If tenant isolation,
   identity documents, credentials, tokens, or personal data may be exposed,
   page security/privacy/legal roles and follow the approved breach process.
4. Preserve evidence: image SHA, Compose release/env-file version, host audit
   logs, Docker/container logs, Caddy logs, SQL audit/backup records, MinIO
   access logs, RabbitMQ delivery state, application logs, audit events, and
   deployment approvals. Redact secrets and document contents.
5. Communicate factual impact, start time, containment, next update, and user
   action. Do not speculate about cause.

## Technical triage

- Check public Caddy/web paths and API /health/live and /health/ready.
- Distinguish process failure from SQL, Redis, RabbitMQ, MinIO/S3, SMTP, SMS,
  maps, scanner, DNS, certificate, firewall, disk, memory, or quota failure.
- Correlate errors by trace ID, image SHA, container restart time, and recent
  deployment/secret/migration events.
- Check SQL blocking/connections, migration history, outbox/dead-letter depth,
  RabbitMQ consumer acknowledgements, worker restarts, Redis availability,
  object authorization, and mail delivery.
- Reproduce with a synthetic account and deliberately separate tenant. Never
  use a customer identity document or copied personal data.
- Follow rollback.md for app rollback and backup-and-restore.md for recovery.

## Closure

Close only after service and data integrity are verified, affected tenants are
notified, privacy/security assessment is complete, monitoring is stable, and
the incident record includes timeline, evidence, cause, impact, recovery, and
preventive actions. Require a blameless review for P1/P2 and any cross-tenant or
personal-data event.
