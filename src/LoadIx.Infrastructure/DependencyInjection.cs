using LoadIx.Application.Abstractions;
using LoadIx.Application.Contracts;
using LoadIx.Application.Documents;
using LoadIx.Application.Finance;
using LoadIx.Application.Instructions;
using LoadIx.Application.MasterData;
using LoadIx.Application.Notifications;
using LoadIx.Application.Reporting;
using LoadIx.Application.Security;
using LoadIx.Application.Trips;
using LoadIx.Infrastructure.Contracts;
using LoadIx.Infrastructure.Documents;
using LoadIx.Infrastructure.Finance;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Instructions;
using LoadIx.Infrastructure.MasterData;
using LoadIx.Infrastructure.Notifications;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Reporting;
using LoadIx.Infrastructure.Seed;
using LoadIx.Infrastructure.Tenancy;
using LoadIx.Infrastructure.Trips;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace LoadIx.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<LoadIxDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
                sqlServer.MigrationsAssembly(typeof(LoadIxDbContext).Assembly.FullName)));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.User.RequireUniqueEmail = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<LoadIxDbContext>()
            .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>(
                TokenOptions.DefaultProvider);
        services.AddScoped<CurrentRequestContext>();
        services.AddScoped<ICurrentRequestContext>(
            serviceProvider => serviceProvider.GetRequiredService<CurrentRequestContext>());
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddScoped<DevelopmentIdentitySeeder>();
        services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
        services.AddScoped<SecurityAuditWriter>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<ILoadInstructionService, LoadInstructionService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISmsNotificationSender, LocalSmsNotificationSender>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<ReportExportService>();
        services.AddScoped<IReportExportService>(serviceProvider =>
            serviceProvider.GetRequiredService<ReportExportService>());
        services.AddSingleton<IReportExportPublisher, RabbitMqReportExportPublisher>();
        services.AddHostedService<ReportExportWorker>();
        services.AddHostedService<ReportExportRepublisher>();
        services.AddSingleton<IMinioClient>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<DocumentStorageOptions>>()
                .Value;
            var builder = new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey);
            if (options.UseSsl)
            {
                builder = builder.WithSSL();
            }

            return builder.Build();
        });
        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        services.AddSingleton<IMalwareScanner>(serviceProvider =>
            string.Equals(
                serviceProvider
                    .GetRequiredService<IOptions<DocumentStorageOptions>>()
                    .Value
                    .MalwareScanner,
                "DevelopmentSignature",
                StringComparison.OrdinalIgnoreCase)
                ? new DevelopmentSignatureMalwareScanner()
                : new UnconfiguredMalwareScanner());

        return services;
    }
}
