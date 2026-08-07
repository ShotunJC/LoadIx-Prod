using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using LoadIx.Api.Configuration;
using LoadIx.Api.Endpoints;
using LoadIx.Api.Errors;
using LoadIx.Api.Health;
using LoadIx.Api.Observability;
using LoadIx.Api.Operations;
using LoadIx.Api.Security;
using LoadIx.Application;
using LoadIx.Application.Reporting;
using LoadIx.Application.Security;
using LoadIx.Application.System;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure;
using LoadIx.Infrastructure.Documents;
using LoadIx.Infrastructure.Edge;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using LoadIxDataProtectionOptions = LoadIx.Api.Configuration.DataProtectionOptions;
using SchemeOptions = Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;

const string apiVersion = "v1";
const string webCorsPolicy = "LoadIxWeb";

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options =>
    options.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "LoadIx.Api"));

builder.Services
    .AddOptions<LoadIxOptions>()
    .Bind(builder.Configuration.GetSection(LoadIxOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => Uri.TryCreate(options.WebOrigin, UriKind.Absolute, out _),
        "LoadIx:WebOrigin must be an absolute URI.")
    .Validate(
        options => builder.Environment.IsDevelopment()
            || Uri.TryCreate(
                options.WebOrigin,
                UriKind.Absolute,
                out var origin)
                && origin.Scheme == Uri.UriSchemeHttps,
        "LoadIx:WebOrigin must use HTTPS outside Development.")
    .Validate(
        options => string.IsNullOrWhiteSpace(options.TrustedProxyIp)
            || IPAddress.TryParse(options.TrustedProxyIp, out _),
        "LoadIx:TrustedProxyIp must be a valid IP address when configured.")
    .ValidateOnStart();

builder.Services
    .AddOptions<DependencyOptions>()
    .Bind(builder.Configuration.GetSection(DependencyOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => builder.Environment.IsDevelopment()
            || options.RedisUseSsl
            || builder.Configuration.GetValue<bool>(
                $"{LoadIxOptions.SectionName}:AllowInsecurePrivateDependencies"),
        "Redis TLS is required unless explicitly limited to trusted private-host dependencies.")
    .ValidateOnStart();
builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => builder.Environment.IsDevelopment()
            || options.RabbitMqUseSsl
            || builder.Configuration.GetValue<bool>(
                $"{LoadIxOptions.SectionName}:AllowInsecurePrivateDependencies"),
        "RabbitMQ TLS is required unless explicitly limited to trusted private-host dependencies.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(AuthenticationOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => builder.Environment.IsDevelopment()
            || !options.SigningKey.Contains(
                "development-only",
                StringComparison.OrdinalIgnoreCase),
        "A deployment-specific authentication signing key is required outside Development.")
    .Validate(
        options => builder.Environment.IsDevelopment()
            || options.AccessTokenMinutes <= 15,
        "Access tokens must expire within 15 minutes outside Development.")
    .ValidateOnStart();
builder.Services
    .AddOptions<AccountEmailOptions>()
    .Bind(builder.Configuration.GetSection(AccountEmailOptions.SectionName))
    .Validate(options =>
            !string.IsNullOrWhiteSpace(options.Host)
            && options.Port is > 0 and <= 65535
            && Uri.CheckHostName(options.FromAddress.Split('@').Last())
                != UriHostNameType.Unknown
            && string.IsNullOrWhiteSpace(options.Username)
                == string.IsNullOrWhiteSpace(options.Password)
            && (builder.Environment.IsDevelopment() || options.EnableSsl),
        "Account email configuration is invalid.")
    .ValidateOnStart();
builder.Services
    .AddOptions<DocumentStorageOptions>()
    .Bind(builder.Configuration.GetSection(DocumentStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => string.Equals(
            options.Provider,
            "S3Compatible",
            StringComparison.OrdinalIgnoreCase),
        "Documents:Provider must be S3Compatible.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Endpoint)
            && !string.IsNullOrWhiteSpace(options.AccessKey)
            && !string.IsNullOrWhiteSpace(options.SecretKey)
            && (builder.Environment.IsDevelopment()
                || options.UseSsl
                || builder.Configuration.GetValue<bool>(
                    $"{LoadIxOptions.SectionName}:AllowInsecurePrivateDependencies")),
        "Document storage credentials and endpoint are invalid for the selected provider.")
    .Validate(
        options => builder.Environment.IsDevelopment()
            || !string.Equals(
                options.MalwareScanner,
                "DevelopmentSignature",
                StringComparison.OrdinalIgnoreCase),
        "A production malware scanner adapter must be configured outside Development.")
    .ValidateOnStart();
builder.Services
    .AddOptions<EdgeDeviceOptions>()
    .Bind(builder.Configuration.GetSection(EdgeDeviceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<LoadIxDataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(LoadIxDataProtectionOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => string.Equals(
            options.Provider,
            "FileSystem",
            StringComparison.OrdinalIgnoreCase),
        "DataProtection:Provider must be FileSystem.")
    .Validate(
        options => builder.Environment.IsDevelopment()
            || Path.IsPathFullyQualified(options.KeyRingPath),
        "Data Protection key storage configuration is invalid.")
    .Validate(
        options => builder.Environment.IsDevelopment()
            || string.Equals(
                options.Provider,
                "FileSystem",
                StringComparison.OrdinalIgnoreCase),
        "Persistent filesystem Data Protection is required outside Development.")
    .ValidateOnStart();

var documentOptions = builder.Configuration
    .GetRequiredSection(DocumentStorageOptions.SectionName)
    .Get<DocumentStorageOptions>()
    ?? throw new InvalidOperationException("Document storage configuration is required.");
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit =
        documentOptions.MaximumSizeBytes + (1024L * 1024L));

var loadIxOptions = builder.Configuration
    .GetRequiredSection(LoadIxOptions.SectionName)
    .Get<LoadIxOptions>()
    ?? throw new InvalidOperationException("LoadIx configuration is required.");

var dataProtectionOptions = builder.Configuration
    .GetRequiredSection(LoadIxDataProtectionOptions.SectionName)
    .Get<LoadIxDataProtectionOptions>()
    ?? throw new InvalidOperationException(
        "Data Protection configuration is required.");

var dependencyOptions = builder.Configuration
    .GetRequiredSection(DependencyOptions.SectionName)
    .Get<DependencyOptions>()
    ?? throw new InvalidOperationException("Dependency configuration is required.");

var databaseConnectionString = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    throw new InvalidOperationException(
        "The ConnectionStrings:Database setting is required.");
}

builder.Services.AddApplication(new SystemInfoDescriptor(
    loadIxOptions.ServiceName,
    loadIxOptions.Version,
    apiVersion));
builder.Services.AddInfrastructure(
    databaseConnectionString,
    loadIxOptions.RunBackgroundWorkers);

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionOptions.ApplicationName);
Directory.CreateDirectory(dataProtectionOptions.KeyRingPath);
dataProtection.PersistKeysToFileSystem(
    new DirectoryInfo(dataProtectionOptions.KeyRingPath));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    if (IPAddress.TryParse(loadIxOptions.TrustedProxyIp, out var trustedProxy))
    {
        options.KnownProxies.Add(trustedProxy);
    }
});
builder.Services.AddScoped<IdentityTokenService>();
builder.Services.AddScoped<TenantContextMiddleware>();
builder.Services.AddSingleton<EdgeDeviceRateLimiter>();

var authenticationOptions = builder.Configuration
    .GetRequiredSection(AuthenticationOptions.SectionName)
    .Get<AuthenticationOptions>()
    ?? throw new InvalidOperationException("Authentication configuration is required.");
var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(authenticationOptions.SigningKey));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authenticationOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authenticationOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/operations"))
                {
                    context.Token = context.Request.Query["access_token"];
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(
                    JwtRegisteredClaimNames.Sub)?.Value;
                var sessionIdValue = context.Principal?.FindFirst(
                    LoadIxClaimTypes.SessionId)?.Value;
                var securityStamp = context.Principal?.FindFirst(
                    LoadIxClaimTypes.SecurityStamp)?.Value;
                if (!Guid.TryParse(userIdValue, out var userId)
                    || !Guid.TryParse(sessionIdValue, out var sessionId))
                {
                    context.Fail("Invalid authentication state.");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices
                    .GetRequiredService<LoadIxDbContext>();
                var timeProvider = context.HttpContext.RequestServices
                    .GetRequiredService<TimeProvider>();
                var now = timeProvider.GetUtcNow();
                var userState = await dbContext.Users
                    .AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => new
                    {
                        user.TenantId,
                        user.IsActive,
                        user.EmailConfirmed,
                        user.SecurityStamp,
                    })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                var claimedTenantId = context.Principal?.FindFirst(
                    LoadIxClaimTypes.TenantId)?.Value;
                var tenantMatches = userState is not null
                    && ((userState.TenantId is null && claimedTenantId is null)
                        || (userState.TenantId is { } userTenant
                            && Guid.TryParse(claimedTenantId, out var claimedTenant)
                            && userTenant == TenantId.From(claimedTenant)));
                var isValid = userState is not null
                    && userState.IsActive
                    && userState.EmailConfirmed
                    && userState.SecurityStamp == securityStamp
                    && tenantMatches
                    && await dbContext.RefreshSessions.AnyAsync(session =>
                            session.Id == sessionId
                            && session.UserId == userId
                            && session.TenantId == userState.TenantId
                            && session.RevokedAtUtc == null
                            && session.ExpiresAtUtc > now,
                        context.HttpContext.RequestAborted);
                if (!isValid)
                {
                    context.HttpContext.Items["LoadIx.TokenAuditWritten"] = true;
                    var auditWriter = context.HttpContext.RequestServices
                        .GetRequiredService<SecurityAuditWriter>();
                    await auditWriter.TryWriteAsync(
                        "Authentication.TokenRejected",
                        "Failed",
                        "Session",
                        sessionId.ToString(),
                        userState?.TenantId,
                        userId,
                        false,
                        context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                        context.HttpContext.RequestAborted,
                        failureCode: "inactive_or_mismatched_session");
                    context.Fail("The session is no longer active.");
                }
            },
            OnAuthenticationFailed = async context =>
            {
                if (context.HttpContext.Items.ContainsKey("LoadIx.TokenAuditWritten"))
                {
                    return;
                }

                var auditWriter = context.HttpContext.RequestServices
                    .GetRequiredService<SecurityAuditWriter>();
                await auditWriter.TryWriteAsync(
                    "Authentication.TokenRejected",
                    "Failed",
                    "Session",
                    null,
                    null,
                    null,
                    false,
                    context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    context.HttpContext.RequestAborted,
                    failureCode: context.Exception is SecurityTokenExpiredException
                        ? "expired_token"
                        : "token_validation_failed");
            },
        };
    })
    .AddScheme<SchemeOptions, EdgeDeviceAuthenticationHandler>(
        EdgeDeviceAuthenticationDefaults.Scheme,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in LoadIxPermissions.All)
    {
        options.AddPolicy(
            permission,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    context.User.HasClaim(
                        LoadIxClaimTypes.Permission,
                        permission)
                    || context.User.HasClaim(
                        LoadIxClaimTypes.PlatformAdministrator,
                        bool.TrueString)));
    }
    options.AddPolicy(
        EdgeDeviceAuthenticationDefaults.Policy,
        policy => policy
            .AddAuthenticationSchemes(EdgeDeviceAuthenticationDefaults.Scheme)
            .RequireAuthenticatedUser()
            .RequireClaim(LoadIxClaimTypes.EdgeDeviceId));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext =>
        {
            if (!httpContext.Request.Path.StartsWithSegments("/api/v1"))
            {
                return RateLimitPartition.GetNoLimiter<string>("non-api");
            }

            var subject = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var partition = string.IsNullOrWhiteSpace(subject)
                ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                : $"user:{subject}";
            return RateLimitPartition.GetFixedWindowLimiter(
                partition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Environment.IsDevelopment() ? 500 : 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        });
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "The request rate limit has been exceeded.",
                Type = "https://httpstatuses.com/429",
                Instance = context.HttpContext.Request.Path,
            },
            cancellationToken);
    };
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 30 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("password-recovery", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            }));
    options.AddPolicy("user-administration", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("document-upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 30 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("edge-bootstrap", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 20 : 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            }));
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd(
            "traceId",
            Activity.Current?.TraceId.ToString()
            ?? context.HttpContext.TraceIdentifier);
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(apiVersion);
builder.Services
    .AddSignalR()
    .AddLoadIxRedisBackplane(dependencyOptions);
if (loadIxOptions.RunBackgroundWorkers)
{
    builder.Services.AddHostedService<OperationalOutboxDispatcher>();
    builder.Services.AddHostedService<NotificationDeliveryDispatcher>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        webCorsPolicy,
        policy => policy
            .WithOrigins(loadIxOptions.WebOrigin)
            .WithHeaders(
                "Authorization",
                "Content-Type",
                "If-Match",
                "Idempotency-Key",
                "X-LoadIx-Client",
                TenantContextMiddleware.SupportTenantHeader,
                TenantContextMiddleware.SupportReasonHeader,
                EdgeDeviceAuthenticationDefaults.DeviceIdHeader,
                EdgeDeviceAuthenticationDefaults.SecretHeader,
                EdgeDeviceAuthenticationDefaults.TimestampHeader,
                EdgeDeviceAuthenticationDefaults.NonceHeader)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .AllowCredentials());
});

builder.Services
    .AddHealthChecks()
    .AddCheck<StartupHealthCheck>("self", tags: ["live"])
    .AddCheck(
        "sql-server",
        new SqlServerHealthCheck(databaseConnectionString),
        tags: ["ready"])
    .AddCheck(
        "redis",
        new TcpEndpointHealthCheck(
            "Redis",
            dependencyOptions.RedisHost,
            dependencyOptions.RedisPort),
        tags: ["ready"])
    .AddCheck(
        "rabbitmq",
        new TcpEndpointHealthCheck(
            "RabbitMQ",
            dependencyOptions.RabbitMqHost,
            dependencyOptions.RabbitMqPort),
        tags: ["ready"])
    .AddCheck(
        "object-storage",
        new TcpEndpointHealthCheck(
            "Object storage",
            dependencyOptions.ObjectStorageHost,
            dependencyOptions.ObjectStoragePort),
        tags: ["ready"])
    .AddCheck(
        "smtp",
        new TcpEndpointHealthCheck(
            "SMTP",
            dependencyOptions.SmtpHost,
            dependencyOptions.SmtpPort),
        tags: ["ready"]);

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(loadIxOptions.ServiceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddSource(LoadIx.Infrastructure.Observability.LoadIxTelemetry.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(LoadIx.Infrastructure.Observability.LoadIxTelemetry.MeterName));

if (!string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services
        .AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddOtlpExporter())
        .WithMetrics(metrics => metrics.AddOtlpExporter());
}

var app = builder.Build();

if (args.Contains("--seed-development", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Development seed data can only be applied in Development.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
    var identitySeeder =
        scope.ServiceProvider.GetRequiredService<DevelopmentIdentitySeeder>();
    await identitySeeder.SeedAsync(CancellationToken.None);
    return;
}

if (args.Contains("--seed-performance", StringComparer.OrdinalIgnoreCase))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Performance seed data can only be applied in Development.");
    }

    var password = Environment.GetEnvironmentVariable(
        "LOADIX_PERFORMANCE_SEED_PASSWORD");
    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "LOADIX_PERFORMANCE_SEED_PASSWORD must be set for the performance seed command.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<PerformanceDataSeeder>();
    await seeder.SeedAsync(password, CancellationToken.None);
    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseMiddleware<TraceCorrelationMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors(webCorsPolicy);
app.UseRateLimiter();
app.UseMiddleware<EdgeDeviceRateLimitingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
    });

var versionedApi = app.MapGroup("/api/v1");
versionedApi.WithTags("System").MapSystemEndpoints();
versionedApi.MapAuthEndpoints(app.Environment);
versionedApi.MapUserAdminEndpoints();
versionedApi.MapMasterDataEndpoints();
versionedApi.MapContractEndpoints();
versionedApi.MapLoadInstructionEndpoints();
versionedApi.MapTripEndpoints();
versionedApi.MapDocumentEndpoints();
versionedApi.MapOperationsEndpoints();
versionedApi.MapNotificationEndpoints();
versionedApi.MapFinanceEndpoints();
versionedApi.MapReportingEndpoints();
versionedApi.MapGovernanceEndpoints();
versionedApi.MapEdgeEndpoints();
app.MapHub<OperationsHub>("/hubs/operations");

app.Run();

public partial class Program;
