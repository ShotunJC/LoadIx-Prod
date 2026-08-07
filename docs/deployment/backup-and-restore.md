# Pilot backup and restore

Backups protect SQL data, document objects, Data Protection keys, and the
release configuration. They do not replace tenant isolation, retention policy,
or a tested restore. The pilot owner must approve the final RPO/RTO; use RPO
15 minutes and RTO 4 hours as planning targets until a rehearsal measures them.

## Policy

- SQL Server: configure encrypted full backups, frequent differential/log
  backups where supported, approved retention for POPIA/legal hold, off-host
  copies, integrity checks, and failed-backup alerts. Never rely only on a
  Docker volume snapshot.
- MinIO/S3: keep the document bucket private, enable versioning/object-lock or
  legal hold only after policy approval, encrypt at rest, and replicate or copy
  objects to another failure domain. Back up object metadata and test an
  authorized download after restore.
- Data Protection: include the api-data-protection volume in encrypted off-host
  backups. Losing it can invalidate protected cookies and token-protected
  values across releases.
- Caddy: back up Caddy data/config only as operational state; certificates can
  be recreated after DNS is correct.
- Redis: treat as cache/coordination state, not the system of record.
- RabbitMQ: use durable queues and dead-letter queues. The SQL outbox remains
  authoritative; record broker definitions, queue durability, and backup
  behavior separately.
- /etc/loadix/pilot.env: back up an encrypted configuration envelope or secret
  references, never an unprotected copy of secret values.
- Logs/telemetry: retain the minimum operational and audit evidence approved
  for the pilot; redact personal data and document contents.

Check backup age, retention, storage capacity, and alerts daily. Rehearse a
restore before go-live, monthly during an active pilot, and after material
schema or retention changes.

## Restore procedure

1. Declare the incident, freeze destructive actions, record UTC time, affected
   tenants, suspected failure, and the RPO/RTO clock. Do not restore over the
   only copy.
2. Restrict traffic with the approved maintenance page or Caddy firewall rule.
   Keep the last compatible application images available.
3. Select a SQL restore point, matching object versions/replica, Data Protection
   key volume copy, and configuration release. Confirm legal-hold implications.
4. Restore SQL to a separate controlled database or host. Apply firewall,
   encryption, certificate validation, auditing, and the migration identity
   before application access.
5. Restore MinIO/S3 objects and metadata to controlled recovery storage. Do not
   delete newer objects until reconciliation is approved. Verify the document
   bucket and Data Protection keys.
6. Point a controlled recovery Compose release at the restored endpoints. Run
   schema checks, tenant/membership counts, audit checks, document metadata/
   object checks, and outbox/dead-letter checks.
7. Run only approved pending migrations. Never let application startup mutate
   the schema.
8. Rebuild Redis if required, reconnect RabbitMQ, inspect acknowledgements and
   duplicate work, and preserve idempotency keys.
9. Run scripts/smoke-test.sh, a cross-tenant denial test, and a document
   download-authorization test. Obtain incident-owner approval before traffic.
10. Monitor for an observation window, notify affected users, and record actual
    data loss, restore duration, provider actions, and follow-up controls.

## Verification checklist

- [ ] SQL is reachable with certificate validation and correct migration history.
- [ ] Tenants, memberships, authorization, and audit evidence are present.
- [ ] MinIO/S3 metadata maps to objects; missing/extra objects are explained.
- [ ] Data Protection keys decrypt existing protected values where expected.
- [ ] Outbox, RabbitMQ queues, report exports, notifications, and worker lag are
      bounded and duplicates are not silently emitted.
- [ ] Health, logs, alerts, SMTP/SMS, maps, and uploads work.
- [ ] Cross-tenant denial and protected-download checks pass.
- [ ] Support and privacy/legal owners approve the outcome.
