using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using LoadIx.Api.Configuration;
using LoadIx.Api.Endpoints;
using LoadIx.Api.Errors;
using LoadIx.Api.Health;
using LoadIx.Api.Operations;
using LoadIx.Api.Security;
using LoadIx.Application;
using LoadIx.Application.Reporting;
using LoadIx.Application.Security;
using LoadIx.Application.System;
using LoadIx.Infrastructure;
using LoadIx.Infrastructure.Documents;
using LoadIx.Infrastructure.Edge;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SchemeOptions = Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;

const string apiVersion = "v1";
const string webCorsPolicy = "LoadIxWeb";

var builder = WebApplication.CreateBuilder(args);

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
    .ValidateOnStart();

builder.Services
    .AddOptions<DependencyOptions>()
    .Bind(builder.Configuration.GetSection(DependencyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .ValidateDataAnnotations()
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
                == string.IsNullOrWhiteSpace(options.Password),
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
builder.Services.AddInfrastructure(databaseConnectionString);
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
                var now = TimeProvider.System.GetUtcNow();
                var isValid = await dbContext.Users.AnyAsync(user =>
                        user.Id == userId
                        && user.IsActive
                        && user.EmailConfirmed
                        && user.SecurityStamp == securityStamp,
                    context.HttpContext.RequestAborted)
                    && await dbContext.RefreshSessions.AnyAsync(session =>
                            session.Id == sessionId
                            && session.UserId == userId
                            && session.RevokedAtUtc == null
                            && session.ExpiresAtUtc > now,
                        context.HttpContext.RequestAborted);
                if (!isValid)
                {
                    context.Fail("The session is no longer active.");
                }
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
            context.HttpContext.TraceIdentifier);
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(apiVersion);
builder.Services.AddSignalR();
builder.Services.AddHostedService<OperationalOutboxDispatcher>();
builder.Services.AddHostedService<NotificationDeliveryDispatcher>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        webCorsPolicy,
        policy => policy
            .WithOrigins(loadIxOptions.WebOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
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
        "minio",
        new TcpEndpointHealthCheck(
            "MinIO",
            dependencyOptions.MinioHost,
            dependencyOptions.MinioPort),
        tags: ["ready"])
    .AddCheck(
        "mailpit",
        new TcpEndpointHealthCheck(
            "Mailpit",
            dependencyOptions.MailpitHost,
            dependencyOptions.MailpitPort),
        tags: ["ready"]);

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(loadIxOptions.ServiceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors(webCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<EdgeDeviceRateLimitingMiddleware>();

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
versionedApi.MapEdgeEndpoints();
app.MapHub<OperationsHub>("/hubs/operations");

app.Run();

public partial class Program;
