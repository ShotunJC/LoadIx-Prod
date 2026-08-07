# Pilot database migrations

Production schema changes are explicit artifacts. API and worker startup never
runs database update.

## Migration image and job

deploy/migrations.Dockerfile builds an EF migration bundle in an SDK build
stage. The final image contains only the bundle and the ASP.NET runtime, runs
as the platform non-root application user, and applies the
LoadIx.Infrastructure migrations. The dedicated-server deployment script runs
the migration image as a one-shot Compose tools service:

    docker compose --env-file /etc/loadix/pilot.env \
      -f compose.pilot.yaml --profile tools run --rm migrate

The image receives the protected migration connection through the env file. It
does not write the connection string into the image or command history.

## Release procedure

1. Add a descriptive EF Core migration. Never edit a migration already applied
   to a shared environment.
2. Review generated SQL, lock duration, index strategy, tenant-key coverage,
   backfill volume, and the forward-fix plan.
3. Run restore, format, release build, unit/architecture/integration tests, and
   the migration against a disposable SQL database.
4. Publish API, web, worker, and migration images with the same immutable SHA.
5. Confirm backup age, SQL connectivity, certificate validity, and change-window
   approval.
6. Start the protected Deploy LoadIx pilot to dedicated server workflow. It
   pulls images and runs migration before application containers change.
7. Capture migration duration, __EFMigrationsHistory result, logs, smoke-test
   evidence, and the release tag. Keep the previous application tag available.

## Migration design rules

- Use expand/contract for changes spanning releases: add nullable fields or
  tables, deploy compatible code, backfill in bounded batches, switch reads,
  then remove old structures in a later release.
- Avoid long blocking locks and table scans during operating hours. Measure
  migrations on pilot-sized data.
- Only one protected deployment may run a schema writer. Check
  __EFMigrationsHistory before retrying a failed job.
- The runtime SQL identity should not retain broad DDL rights after bootstrap.
  Use a separately controlled migration credential where SQL policy permits.
- Do not run EF Down migrations automatically in production. Prefer additive
  forward fixes; use backup/restore only for a confirmed recovery incident.
- Preserve approval, SQL review, execution result, and smoke evidence with the
  change record.

## Failure handling

If migration fails, application containers remain on the previous release.
Inspect the migration image logs and __EFMigrationsHistory. Determine whether
the failure happened before or after a transaction boundary; do not blindly
rerun a non-idempotent data script. Use a reviewed forward fix or the restore
procedure. The deployment script restores the previous image tag, but it does
not undo a partially applied schema.
