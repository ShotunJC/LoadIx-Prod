# Pilot rollback procedure

Rollback is a controlled recovery action. First classify the fault as code,
configuration, dependency, schema, data, or security-related.

## Application-only rollback

Use this when the previous code is compatible with the current database schema.

1. Record the current and known-good image SHA, migration history, env-file
   release, alerts, logs, and deployment approval.
2. Stop or pause the rollout. Do not scale the worker while investigating
   duplicate or poisoned messages.
3. Start the protected Roll back LoadIx pilot workflow with the known-good
   image tag and HTTPS base URL.
4. The workflow checks out that commit on the dedicated server, changes only the
   env-file image tag, pulls API/web/worker images, and restarts those services
   without running a migration.
5. Run the HTTPS smoke test and focused login, authorization, tenant-isolation,
   document-access, broker, outbox, and worker checks. Keep faulty images
   available for evidence.

The rollback script does not change SQL schema. Docker images are immutable
commit-SHA tags; do not rebuild an old tag in place.

## Migration or data rollback

- If migration fails before traffic changes, leave the previous application in
  service, inspect __EFMigrationsHistory, and use a reviewed forward fix.
- If a successful migration is incompatible with the previous application,
  deploy the compatible application or a forward-fix migration. Do not send old
  code at new schema without review.
- Never run EF Down migrations automatically in production. They can destroy
  data written by the new release and cannot reverse external effects.
- If data is corrupted or deleted, use backup-and-restore. Restore SQL and
  MinIO/S3 into controlled recovery resources, reconcile object metadata and
  outbox state, and obtain business/privacy approval before cutover.

## Configuration and security rollback

Restore the last known-good protected env-file version, recreate the affected
containers, and revoke the compromised credential. Never re-enable a leaked
secret merely to make a smoke test pass. If compromise is possible, restrict
Caddy/SSH access, preserve host/container/audit evidence, rotate credentials,
and follow incident-response.md.

## Exit criteria

- [ ] HTTPS traffic reaches the known-good API/web application.
- [ ] Database schema is compatible with active code.
- [ ] RabbitMQ, outbox, notifications, report exports, and worker are healthy.
- [ ] Smoke and focused authorization/tenant-isolation checks pass.
- [ ] No new secret, document, or personal-data exposure is suspected.
- [ ] Stakeholders have status and the release record has owner, timestamps,
      evidence, and follow-up actions.
