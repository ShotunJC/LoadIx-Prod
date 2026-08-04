# LoadIx pilot operations runbook

This runbook is for the dedicated Linux server and Docker Compose pilot release.
Commands assume the repository is at the configured VPS_DEPLOY_PATH and the
protected env file is /etc/loadix/pilot.env. Do not paste secret values into
commands or support chat.

## Daily checks

1. Verify host patch status, disk/inode capacity, CPU/memory, Docker daemon,
   certificate expiry, backup age, and failed-backup alerts.
2. Check service state and recent logs:

   docker compose --env-file /etc/loadix/pilot.env \
   -f compose.pilot.yaml ps
   docker compose --env-file /etc/loadix/pilot.env \
   -f compose.pilot.yaml logs --tail=100 api web worker caddy

3. Check public health from an approved workstation:

   sh scripts/smoke-test.sh https://pilot.example.com

4. Check SQL connectivity, RabbitMQ queue/dead-letter depth, worker lag,
   MinIO/S3 errors, SMTP delivery, and application error/latency dashboards.
5. Confirm no secrets, tokens, identity numbers, or document contents appear in
   logs.

## Standard release

1. Confirm change approval, backup age, migration SQL review, rollback tag,
   support/on-call coverage, and pilot checklist status.
2. Run the Containers workflow and record the four published image tags/digests.
3. Start the protected Deploy LoadIx pilot to dedicated server workflow.
4. Observe migrations, container restarts, readiness, SQL, RabbitMQ depth,
   worker processing, uploads, notifications, and audit events.
5. Run the smoke test and a representative tenant workflow. Keep the previous
   tag until the observation window closes.

## Useful operations

Restart only a service after diagnosis:

    docker compose --env-file /etc/loadix/pilot.env \
      -f compose.pilot.yaml up -d api

Inspect state without printing the env file:

    docker compose --env-file /etc/loadix/pilot.env \
      -f compose.pilot.yaml ps

Follow one service:

    docker compose --env-file /etc/loadix/pilot.env \
      -f compose.pilot.yaml logs --follow --tail=200 worker

Do not use docker compose down in an incident unless the incident commander
approves it; it removes the normal health/restart boundary and may interrupt
recovery.

## Failed deployment

1. Leave public traffic on the last healthy application if migration failed.
2. Preserve migration/container/Caddy logs and __EFMigrationsHistory evidence.
3. If schema is compatible, start Roll back LoadIx pilot with the known-good SHA.
4. If schema/data is not compatible, stop and use a reviewed forward-fix or
   backup-and-restore procedure.
5. Run smoke and focused authorization/tenant-isolation checks before closure.

## Incident and support

Open an incident record, assign the incident commander, preserve evidence, and
use incident-response.md. Verify tenant identity before discussing account
details. Escalate cross-tenant, credential, document, or POPIA concerns
immediately to security/privacy/legal owners.

## Go-live handoff

Store the completed pilot checklist, contact list, DPA/privacy decision,
training evidence, backup/restore rehearsal, smoke result, release SHA, and
rollback owner in the protected operations system. Do not store those
documents with customer personal data in this repository.
