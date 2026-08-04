# LoadIx pilot deployment foundation

The default pilot target is a dedicated Linux server using Docker Compose and
Caddy. This is the hardened successor to the current VPS deployment in
.github/workflows/deploy.yml; that workflow remains a synthetic demo path.
The pilot workflow is protected, SSH-based, and does not deploy to a cloud
account.

The application remains provider-portable. The pilot uses RabbitMQ for AMQP
0-9-1 messaging and MinIO or another S3-compatible service for document
objects. Azure Service Bus and Azure Blob are not required by this deployment.

## Target topology

    Internet
       |
    DNS A/AAAA -> Caddy (80/443, automatic ACME certificate)
       |
    web container (Nginx, non-root)
       |  /api, /health, /hubs -> API container (private Compose network)
       +--> static React application

    API container (non-root, no public port)
       |--> SQL Server / approved SQL endpoint (TLS)
       |--> Redis (private endpoint, TLS where supported)
       |--> RabbitMQ (AMQPS 5671, durable queues)
       |--> MinIO/S3-compatible object storage (HTTPS)
       +--> approved SMTP, SMS, map, malware-scanner, and telemetry providers

    Worker container (non-root, no public port)
       +--> same SQL, Redis, RabbitMQ, object-storage, and provider endpoints

    Migration image (one-shot, protected workflow step)

Only Caddy publishes ports 80 and 443. SSH is restricted to the administration
network. SQL Server, Redis, RabbitMQ, management consoles, and MinIO consoles
must not be published on the public interface.

compose.pilot.yaml intentionally starts only the application edge/API/worker
and migration containers. Stateful dependencies are supplied as already
secured private endpoints so the pilot cannot silently start a Developer SQL
edition, plaintext broker, or unprotected object store. They may be installed
on the dedicated server in a separately reviewed stack, or provided by an
approved private service, but their certificates, credentials, backups, and
retention must be configured before the pilot env file is created.

## Dedicated-server prerequisites

Before enabling the protected pilot GitHub environment, record an owner and
complete these gates:

1. Provision a supported Linux dedicated server with a fixed address, encrypted
   disks or equivalent host control, current security updates, Docker Engine,
   and Docker Compose v2.
2. Point the pilot DNS A/AAAA record at the server. Allow inbound TCP 80/443
   and SSH only from approved administration addresses. Enable host firewall,
   unattended security updates, time synchronisation, and intrusion/credential
   monitoring.
3. Create a non-root deployment account with narrowly scoped Docker/sudo access.
   Store the repository at the protected VPS_DEPLOY_PATH and keep the checkout
   free of local tracked changes.
4. Create /etc/loadix/pilot.env from .env.pilot.example, fill it through the
   approved secret process, make it owned by root (or the deployment account)
   and mode 0600, and never copy it into GitHub logs.
5. Provide a certificate-validating SQL Server endpoint, Redis endpoint,
   RabbitMQ AMQPS endpoint, and HTTPS MinIO/S3-compatible endpoint. A service
   may run on the same host only when its port is private and its TLS/certificate
   configuration is tested. RabbitMQ must remain RabbitMQ; a different broker
   requires a separately approved adapter.
6. Configure private object storage with the loadix-documents bucket and a
   separate Data Protection key location if the object-storage provider is also
   used for backups. MinIO is the current dedicated-server choice.
7. Obtain approved SMTP/SMS/map/malware-scanner/telemetry providers and record
   sender, quota, retention, and support details.
8. Configure protected GitHub environments container-registry for GHCR image
   publication and pilot for deployment approvals. Required values are in
   configuration.md.

## Dependency guidance

- **Managed SQL:** a managed SQL Server-compatible service is acceptable when
  it offers TLS certificate validation, private networking or a restricted
  allowlist, point-in-time/encrypted backups, audit logs, capacity alerts, and
  a least-privilege runtime login separate from the migration login. Confirm
  provider SQL Server/version compatibility before the migration rehearsal.
- **Managed Redis:** a managed Redis service is acceptable for cache and
  coordination when it provides TLS, private access, authentication, memory/
  eviction metrics, and an approved failover policy. Redis is never the system
  of record; do not put documents, credentials, or irreversible workflow state
  there.
- **RabbitMQ:** use RabbitMQ directly, either as a protected service on the
  dedicated host or an AMQPS-compatible external service. Use a dedicated vhost,
  non-admin user, durable queues, dead-letter queues, and queue-depth alerts.
  Azure Service Bus is not a drop-in replacement for the current client.
- **Object storage:** use MinIO on the dedicated server or another private
  S3-compatible endpoint over HTTPS. Keep the document bucket private, use
  scoped credentials, version/replicate objects as policy allows, and include
  metadata/object reconciliation in restore rehearsals. Azure Blob is not
  required for this pilot.

## Release sequence

1. Pull requests pass backend/frontend checks, dependency scanning, production
   container builds, and container scanning.
2. Run the Containers workflow from an approved branch. It publishes API, web,
   worker, and migration images to GHCR using the immutable 40-character commit
   SHA. The workflow uses GITHUB_TOKEN; no cloud credential is stored.
3. Confirm current backup age, migration SQL review, incident contacts, support
   coverage, and rollback tag. Start Deploy LoadIx pilot to dedicated server
   with the image SHA and the exact HTTPS base URL.
4. GitHub environment protection approves the SSH deployment. The runner uses a
   protected known-hosts value, logs the server into GHCR, checks out the
   selected commit, and runs scripts/deploy-pilot.sh.
5. The script validates the protected env file, pulls immutable images, runs the
   one-shot migration image, and only then updates API, web, worker, and Caddy.
   On failure it restores the previous env-file release tag and starts the
   previous application images; it never runs an automatic schema rollback.
6. The workflow runs scripts/smoke-test.sh against the public HTTPS URL. Keep
   the previous image tag and release record until the observation window ends.

## Runtime safeguards

- API, web, worker, and migration images are multi-stage and non-root. Production
  images remove development appsettings and disable .NET diagnostics.
- Secrets are read from the server-protected env file and GitHub protected
  environment secrets. They are not Dockerfile args, image layers, Git files,
  command-line arguments, or log output.
- Caddy is the only public application ingress and is responsible for HTTPS and
  certificate renewal. The public origin must match LoadIx__WebOrigin and
  AllowedHosts.
- SQL, RabbitMQ, MinIO/S3, SMTP, and telemetry use certificate-validating TLS.
  Redis is private and must use provider TLS when it has a network endpoint.
  Do not use TrustServerCertificate=True for pilot data.
- Data Protection keys persist in the API volume. This is safe for the
  single-host pilot only; a future multi-host deployment must use a shared,
  protected key store and a reviewed provider adapter.
- DevelopmentSignature malware scanning and development seed credentials are
  rejected outside Development. Customer uploads remain disabled until an
  approved scanner and quarantine process are configured.
- /health/live is a process check. /health/ready checks SQL and dependency
  reachability. A downstream outage must not make liveness restart a healthy
  process.
- The API and worker are separate containers. Keep API background workers off
  and run one or more worker replicas so queue consumers do not multiply during
  API scale-out.

## Capacity and autoscaling considerations

The initial dedicated host is vertically sized from observed CPU, memory, SQL
IOPS, document volume, broker depth, and worker lag. Start with one API, one web,
and one worker container. Set explicit Docker memory/CPU limits after baseline
measurements, and alert before host disk, memory, or inode exhaustion.

Docker Compose does not provide safe multi-host autoscaling. If demand exceeds
the host, add capacity with a reviewed second host or move the affected stateful
service to a managed/private endpoint. Scale stateless API/web containers only
after Data Protection, SQL concurrency, session/token behavior, and Caddy
routing have been reviewed. Scale workers from durable RabbitMQ queue depth and
observed processing time, not CPU alone. Preserve idempotency, leases, and
acknowledgements.

## Pilot acceptance checklist

Record evidence, owner, and UTC completion time for every item.

- [ ] Pilot tenant created with approved legal name, identifier, timezone,
      currency, branches, and retention policy.
- [ ] Administrator invitation delivered, accepted, and tested with a unique
      account; MFA/identity policy confirmed where applicable.
- [ ] Storage configuration verified: private MinIO/S3 bucket, access policy,
      TLS, object authorization, quarantine path, and restore test.
- [ ] Email configuration verified with invitation and reset messages; sender,
      domain, TLS, bounce handling, and provider support are approved.
- [ ] SMS configuration verified with a test notification, sender identity,
      delivery-failure handling, spend limit, and provider contact.
- [ ] Map configuration verified with a licensed provider, API restrictions,
      attribution, quotas, fallback/offline behavior, and no public demo tiles.
- [ ] Document retention, deletion, legal hold, quarantine, and personal-data
      handling approved by the pilot owner and privacy/legal reviewer.
- [ ] SQL, object-storage, and host-volume backups enabled; latest timestamps
      and a restore rehearsal recorded.
- [ ] Monitoring dashboards cover health, latency, errors, disk, CPU, memory,
      SQL, Redis, RabbitMQ depth, worker failures, storage, and audit activity.
- [ ] Alerts have tested recipients, severity, suppression/escalation rules,
      and an on-call owner.
- [ ] Logs are structured, UTC-correlated, access-controlled, retained as
      approved, and contain no secrets, tokens, document content, or ID numbers.
- [ ] Support process defines intake, tenant verification, severity, response
      targets, escalation, status updates, and closure evidence.
- [ ] Incident contacts include pilot owner, technical/server on-call, database
      and storage owners, security/privacy lead, RabbitMQ/SMTP/SMS/maps/scanner
      providers, and legal contact.
- [ ] Data-processing agreement, subprocessor register, POPIA/privacy notice,
      cross-border processing, retention, deletion/access requests, and breach
      responsibilities reviewed with counsel.
- [ ] User training covers administration, contracts, loads, dispatch, driver
      sync, proof of delivery, reconciliation, documents, invoicing support,
      support, and offline recovery.
- [ ] Go-live verification confirms DNS, HTTPS, login/invitation, tenant
      isolation, document access, email/SMS, maps, worker processing, audit
      events, backup age, alert delivery, smoke test, and rollback owner.

## Local validation boundary

No server or cloud deployment is performed by local validation. Run restore,
format, warning-as-error build, unit/architecture tests, frontend checks,
docker compose config, image builds/scans where Docker is available, and the
HTTPS smoke-script syntax check. The existing compose.demo.yaml remains a
Development synthetic-data demonstration and must not be used for pilot data.
