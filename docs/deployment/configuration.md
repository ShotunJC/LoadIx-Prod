# Pilot configuration reference

The dedicated-server pilot reads a protected env file on the host, normally
/etc/loadix/pilot.env. The file is root-owned (or owned by the restricted
deployment account), mode 0600, excluded from Git, and never printed. Compose
receives it with --env-file. Checked-in production appsettings contain
placeholders only.

## Rules

- Set ASPNETCORE_ENVIRONMENT=Production for API, worker, and migration images.
- Use a certificate-validating SQL connection string. Do not use
  TrustServerCertificate=True for pilot data.
- Set the exact HTTPS browser origin in LOADIX_WEB_ORIGIN and include that host
  plus api in LOADIX_ALLOWED_HOSTS. Never use a wildcard.
- Keep LoadIx__RunBackgroundWorkers=false on API and true on the worker.
- Configure the same authenticated Redis endpoint on API and worker; it carries
  the SignalR backplane as well as ephemeral coordination.
- Use RabbitMQ AMQP 0-9-1. RabbitMqUseSsl=true and port 5671 are required for
  provider/network endpoints outside Development.
- Use MinIO or another S3-compatible provider for documents. The current pilot
  default is Documents__Provider=S3Compatible; Azure Blob is not required.
- Use a persistent filesystem Data Protection key volume on this single host.
  Do not scale API across hosts until a shared key-store design is approved.
- Do not use DevelopmentSignature, development seed passwords, Mailpit, or
  appsettings.Development.json in the pilot.
- Record all rotation and configuration changes in the change/incident system.

## Host env file

| Host variable                                 | Application setting                       | Source and notes                                          |
| --------------------------------------------- | ----------------------------------------- | --------------------------------------------------------- |
| LOADIX_IMAGE_REGISTRY                         | Compose image prefix                      | Lowercase GHCR repository, for example ghcr.io/org/loadix |
| LOADIX_IMAGE_TAG                              | LoadIx__Version and image tag             | 40-character immutable Git SHA                            |
| LOADIX_DOMAIN                                 | Caddy site                                | Public DNS name                                           |
| LOADIX_ACME_EMAIL                             | Caddy                                     | Operations mailbox for certificate notices                |
| LOADIX_WEB_ORIGIN                             | LoadIx__WebOrigin                         | Exact value such as https://pilot.example.com             |
| LOADIX_ALLOWED_HOSTS                          | AllowedHosts                              | Public host;api, never *                                  |
| LOADIX_DATABASE_CONNECTION_STRING             | ConnectionStrings__Database               | Protected SQL connection string                           |
| LOADIX_REDIS_HOST / PORT                      | Dependencies__RedisHost / Port            | Private Redis endpoint shared by API and worker           |
| LOADIX_REDIS_USE_SSL / PASSWORD / SERVER_NAME | Dependencies__Redis*                      | TLS/authentication for Redis and the SignalR backplane    |
| LOADIX_ALLOW_INSECURE_PRIVATE_DEPENDENCIES    | LoadIx__AllowInsecurePrivateDependencies  | Same-host private network only; never remote              |
| LOADIX_RABBITMQ_HOST / PORT                   | Dependencies__RabbitMqHost / Port         | RabbitMQ AMQPS endpoint, normally 5671                    |
| LOADIX_RABBITMQ_USERNAME / PASSWORD           | Dependencies__RabbitMqUsername / Password | Least-privilege RabbitMQ user                             |
| LOADIX_RABBITMQ_VHOST                         | Dependencies__RabbitMqVirtualHost         | Dedicated vhost, normally /loadix                         |
| LOADIX_RABBITMQ_USE_SSL                       | Dependencies__RabbitMqUseSsl              | true for pilot                                            |
| LOADIX_RABBITMQ_SERVER_NAME                   | Dependencies__RabbitMqServerName          | Certificate DNS name                                      |
| LOADIX_OBJECT_STORAGE_ENDPOINT                | Documents__Endpoint                       | MinIO/S3 endpoint host:port, no scheme                    |
| LOADIX_OBJECT_STORAGE_ACCESS_KEY / SECRET_KEY | Documents credentials                     | Dedicated bucket-scoped credentials                       |
| LOADIX_OBJECT_STORAGE_USE_SSL                 | Documents__UseSsl                         | true for pilot                                            |
| LOADIX_DOCUMENT_BUCKET                        | Documents__Bucket                         | Private loadix-documents bucket                           |
| LOADIX_MALWARE_SCANNER                        | Documents__MalwareScanner                 | Approved production scanner adapter                       |
| LOADIX_SMTP_*                                 | AccountEmail and dependency SMTP          | Approved SMTP with TLS and sender                         |
| LOADIX_AUTHENTICATION_SIGNING_KEY             | Authentication__SigningKey                | High-entropy protected secret                             |
| LOADIX_OTEL_EXPORTER_OTLP_ENDPOINT            | Telemetry exporter                        | Approved HTTPS telemetry endpoint, if used                |

The compose file maps these host variables to .NET double-underscore
configuration keys. It also sets the API/worker service name and immutable
version. Do not add a secret to a Dockerfile ARG: build arguments can be
persisted in image history.

## Reverse proxy and TLS

Caddy terminates public HTTPS and renews the ACME certificate. Only ports 80
and 443 are published. Caddy proxies to the Nginx web container, which serves
the SPA and forwards /api, /hubs, and /health to the private API. The API
receives Host api from Nginx and X-Forwarded-Host containing the public host.

Use real certificates and certificate validation for SQL, RabbitMQ, MinIO/S3,
SMTP, and telemetry. Restrict service DNS/firewall rules to the server or
private network. Do not expose Redis, RabbitMQ management, SQL Server, or MinIO
console to the Internet.

## Secret handling and rotation

The GitHub pilot environment needs VPS_HOST, VPS_PORT, VPS_USER,
VPS_DEPLOY_PATH, VPS_ENV_FILE, and GHCR_USERNAME as protected variables. Keep
VPS_SSH_KEY, VPS_KNOWN_HOSTS, and GHCR_TOKEN as protected secrets. These are
deployment credentials, not application credentials. Application credentials
remain in the server env file.

For rotation:

1. Create and verify the replacement secret with the provider, without
   printing it.
2. Update the protected server env file atomically and preserve mode 0600.
3. Recreate only the affected API/worker containers with the approved release
   procedure.
4. Run the HTTPS smoke test and a functional provider check.
5. Revoke the old credential after the observation window and record the change.

Rotate JWT signing keys with an overlap plan; changing the key immediately
invalidates active tokens. Rotate database, RabbitMQ, MinIO, SMTP, SSH, and ACME
credentials independently so a failed provider change has a clear rollback.
