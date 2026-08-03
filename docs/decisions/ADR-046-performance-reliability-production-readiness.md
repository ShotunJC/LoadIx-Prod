# ADR-046: Performance and reliability controls for pilot readiness

## Status

Accepted for Milestone 17 implementation; local k6 and representative
query-plan evidence are recorded, while failure drills and deployment-scale
capacity remain operational verification tasks.

## Context

The pilot workload needs predictable bounded reads, retry behavior that does not
amplify provider outages, durable asynchronous work, and evidence that the API
can be exercised against realistic tenant data. The repository has one modular
monolith and several local providers: SQL Server, RabbitMQ, MinIO-compatible
object storage, SMTP/Mailpit, and SignalR clients.

## Decision

- Enable SQL Server transient connection retries with a bounded retry count and
  delay.
- Use one shared transient-operation policy for provider transport failures,
  exponential backoff with jitter, and per-provider circuit breakers. Validation,
  authorization, authentication, and other permanent failures are not retried.
- Add outbox retry scheduling, a five-attempt dead-letter state, and durable
  report-export dead-letter routing. Keep failure details bounded and free of
  secrets or payload contents.
- Remove avoidable N+1 notification delivery queries and replace unbounded
  operational-board/location reads with tenant-scoped, status/date-bounded,
  stable, limited queries. Add indexes for board and latest-location access.
- Export request duration, custom workload gauges, background-job failures,
  notification failures, outbox dead letters, and OpenTelemetry traces through
  the existing observability pipeline.
- Keep large generated data behind an explicit performance-seed command. The
  normal development seed remains small and deterministic.
- Maintain the load test as a k6 script under perf/k6; record environment and
  results separately from threshold definitions so an unrun test cannot be
  mistaken for a performance claim.

## Consequences

The API is more resilient to short provider outages and workers stop retrying
permanent failures forever. Retry state and dead letters add schema and
operational review requirements. Circuit-breaker state is process-local, so a
deployment with multiple API replicas still needs provider-level alerting and a
shared operational view. Outbox delivery and notification workers should receive
database lease/claim hardening before high-volume multi-replica operation.

Performance thresholds are enforceable by CI or a pilot run, but the recorded
results remain local evidence rather than a production capacity claim. See
docs/performance-results.md.
