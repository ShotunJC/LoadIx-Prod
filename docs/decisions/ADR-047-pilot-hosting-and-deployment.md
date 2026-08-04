# ADR-047: Dedicated-server pilot hosting and deployment boundary

## Status

Accepted for Milestone 18 on 2026-08-04. The default pilot target is the
current VPS/dedicated-server path; no server deployment is implied by this
repository change.

## Context

LoadIx needs a repeatable, protected deployment for a secure pilot. The
application has one modular-monolith API, a React web/PWA, durable SQL state,
document objects, Redis coordination/cache, RabbitMQ asynchronous work, and
hosted background consumers. Production must not rely on developer machines,
ephemeral container files, startup migrations, or credentials committed to Git.

The available deployment path is a dedicated Linux server currently operated as
a VPS. The application and operational stack should therefore run with Docker
Compose and a public reverse proxy, while keeping provider choices outside the
domain and application layers.

## Decision

- Run Caddy on the dedicated server as the public TLS edge. It proxies to a
  non-root Nginx web container. The web container serves the SPA and forwards
  API, health, and SignalR paths to the private API.
- Run the API and background consumers as separate non-root containers. API
  instances set LoadIx__RunBackgroundWorkers=false; the worker sets it to true.
- Run EF Core migrations from a separate, one-shot migration image. The
  protected deployment workflow must complete migration before application
  containers change.
- Publish immutable API, web, worker, and migration images to GHCR from GitHub
  Actions. The dedicated server authenticates with a protected package token;
  no cloud credential or application secret is stored in Git.
- Keep application configuration in a root-owned, mode-0600 host env file. SSH
  deployment uses a protected private key and pinned known-hosts content.
- Use RabbitMQ for the existing AMQP 0-9-1 integration. Azure Service Bus is not
  a transparent replacement and is not part of the pilot.
- Use MinIO or another S3-compatible object-storage provider for documents.
  Azure Blob is not a pilot prerequisite. Data Protection keys use a persistent
  host-backed filesystem volume for this single-host pilot.
- Use private/certificate-validating SQL, Redis, RabbitMQ, object-storage, SMTP,
  and telemetry endpoints. No dependency management console is public.
- Use immutable image tags for app-only rollback. Database rollback is a
  reviewed forward fix or controlled restore, never an automatic Down migration.

## Consequences

The pilot aligns with the existing VPS operating model and can be validated
with the same Docker/Compose toolchain used by the operator. It gains explicit
TLS, non-root images, persistent state, separate worker deployment, protected
migrations, smoke tests, and an app rollback path.

A dedicated server makes host patching, disk capacity, firewalling, backups,
monitoring, high availability, and replacement procedures operator
responsibilities. Docker Compose does not provide multi-host autoscaling; a
future move to another provider can reuse the application ports and image
contracts without changing domain/application behavior. The S3-compatible
object-storage adapter remains an infrastructure boundary, not a business-rule
dependency.
