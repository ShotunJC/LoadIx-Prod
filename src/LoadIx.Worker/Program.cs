using System.Net;
using LoadIx.Api.Configuration;
using LoadIx.Api.Operations;
using LoadIx.Application;
using LoadIx.Application.Reporting;
using LoadIx.Application.System;
using LoadIx.Infrastructure;
using LoadIx.Infrastructure.Documents;
using LoadIx.Infrastructure.Edge;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using LoadIxDataProtectionOptions = LoadIx.Api.Configuration.DataProtectionOptions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
    options.ShutdownTimeout = TimeSpan.FromSeconds(30));

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
    .ValidateOnStart();

builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "LoadIx.Worker"));

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
    .Validate(
        options => builder.Environment.IsDevelopment() || options.RabbitMqUseSsl,
        "RabbitMQ TLS is required outside Development.")
    .ValidateOnStart();
builder.Services
    .AddOptions<AccountEmailOptions>()
    .Bind(builder.Configuration.GetSection(AccountEmailOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Host)
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
            && (builder.Environment.IsDevelopment() || options.UseSsl),
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

var loadIxOptions = builder.Configuration
    .GetRequiredSection(LoadIxOptions.SectionName)
    .Get<LoadIxOptions>()
    ?? throw new InvalidOperationException("LoadIx configuration is required.");
var dataProtectionOptions = builder.Configuration
    .GetRequiredSection(LoadIxDataProtectionOptions.SectionName)
    .Get<LoadIxDataProtectionOptions>()
    ?? throw new InvalidOperationException(
        "Data Protection configuration is required.");
var databaseConnectionString = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    throw new InvalidOperationException(
        "The ConnectionStrings:Database setting is required.");
}

builder.Services.AddApplication(new SystemInfoDescriptor(
    "LoadIx.Worker",
    loadIxOptions.Version,
    "worker"));
builder.Services.AddInfrastructure(databaseConnectionString, true);
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionOptions.ApplicationName);
Directory.CreateDirectory(dataProtectionOptions.KeyRingPath);
dataProtection.PersistKeysToFileSystem(
    new DirectoryInfo(dataProtectionOptions.KeyRingPath));
builder.Services.AddSignalR();
builder.Services.AddHostedService<OperationalOutboxDispatcher>();
builder.Services.AddHostedService<NotificationDeliveryDispatcher>();

await builder.Build().RunAsync();
