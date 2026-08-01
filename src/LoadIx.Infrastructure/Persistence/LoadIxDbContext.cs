using LoadIx.Application.Abstractions;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;
using LoadIx.Domain.Documents;
using LoadIx.Domain.Edge;
using LoadIx.Domain.Finance;
using LoadIx.Domain.Fleet;
using LoadIx.Domain.Instructions;
using LoadIx.Domain.Locations;
using LoadIx.Domain.Tenancy;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Documents;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Instructions;
using LoadIx.Infrastructure.Notifications;
using LoadIx.Infrastructure.Operations;
using LoadIx.Infrastructure.Reporting;
using LoadIx.Infrastructure.Trips;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Persistence;

public sealed class LoadIxDbContext(
    DbContextOptions<LoadIxDbContext> options,
    ICurrentRequestContext requestContext,
    TimeProvider timeProvider)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<ContactPerson> ContactPeople => Set<ContactPerson>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Commodity> Commodities => Set<Commodity>();

    public DbSet<TransportContract> TransportContracts => Set<TransportContract>();

    public DbSet<ContractRoute> ContractRoutes => Set<ContractRoute>();

    public DbSet<ContractRate> ContractRates => Set<ContractRate>();

    public DbSet<ContractCommodity> ContractCommodities => Set<ContractCommodity>();

    public DbSet<LoadInstruction> LoadInstructions => Set<LoadInstruction>();

    public DbSet<GeneratedTrip> GeneratedTrips => Set<GeneratedTrip>();

    public DbSet<TripGenerationOperation> TripGenerationOperations =>
        Set<TripGenerationOperation>();

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<TripTransition> TripTransitions => Set<TripTransition>();

    public DbSet<TripAssignment> TripAssignments => Set<TripAssignment>();

    public DbSet<TripCommandOperation> TripCommandOperations =>
        Set<TripCommandOperation>();

    public DbSet<TripLocationUpdate> TripLocationUpdates =>
        Set<TripLocationUpdate>();

    public DbSet<OperationalOutboxMessage> OperationalOutboxMessages =>
        Set<OperationalOutboxMessage>();

    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<TenantNotificationTemplate> TenantNotificationTemplates => Set<TenantNotificationTemplate>();

    public DbSet<OperationalDocument> Documents => Set<OperationalDocument>();

    public DbSet<TenantTaxConfiguration> TenantTaxConfigurations =>
        Set<TenantTaxConfiguration>();

    public DbSet<TripFinancials> TripFinancials => Set<TripFinancials>();

    public DbSet<CostEntry> CostEntries => Set<CostEntry>();

    public DbSet<InvoiceBatch> InvoiceBatches => Set<InvoiceBatch>();

    public DbSet<InvoiceBatchLine> InvoiceBatchLines => Set<InvoiceBatchLine>();

    public DbSet<FinancialApproval> FinancialApprovals => Set<FinancialApproval>();

    public DbSet<ReportExportJob> ReportExportJobs => Set<ReportExportJob>();

    public DbSet<DocumentUploadOperation> DocumentUploadOperations =>
        Set<DocumentUploadOperation>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Trailer> Trailers => Set<Trailer>();

    public DbSet<EdgeDevice> EdgeDevices => Set<EdgeDevice>();

    public DbSet<EdgeDeviceCredential> EdgeDeviceCredentials =>
        Set<EdgeDeviceCredential>();

    public DbSet<WeighbridgeDevice> WeighbridgeDevices =>
        Set<WeighbridgeDevice>();

    public DbSet<RawWeightReading> RawWeightReadings =>
        Set<RawWeightReading>();

    public DbSet<StableWeightReading> StableWeightReadings =>
        Set<StableWeightReading>();

    public DbSet<EdgeCommand> EdgeCommands => Set<EdgeCommand>();

    public DbSet<EdgeTicketSubmission> EdgeTicketSubmissions =>
        Set<EdgeTicketSubmission>();

    public DbSet<DeviceHeartbeat> DeviceHeartbeats =>
        Set<DeviceHeartbeat>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Ignore<DomainEvent>();
        builder.ApplyConfigurationsFromAssembly(typeof(LoadIxDbContext).Assembly);

        builder.Entity<Branch>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<UserProfile>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<Customer>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<ContactPerson>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<Location>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<Commodity>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<TransportContract>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<ContractRoute>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<ContractRate>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<ContractCommodity>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<LoadInstruction>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<GeneratedTrip>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<TripGenerationOperation>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<Trip>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<TripTransition>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<TripAssignment>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<TripCommandOperation>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<TripLocationUpdate>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<OperationalDocument>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<TenantTaxConfiguration>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<TripFinancials>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<CostEntry>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<InvoiceBatch>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<InvoiceBatchLine>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<FinancialApproval>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<ReportExportJob>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<DocumentUploadOperation>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<NotificationMessage>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<NotificationDelivery>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<UserNotificationPreference>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<TenantNotificationTemplate>()
            .HasQueryFilter(entity => requestContext.TenantId == entity.TenantId);
        builder.Entity<Driver>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<Vehicle>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<Trailer>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<EdgeDevice>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<EdgeDeviceCredential>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<WeighbridgeDevice>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<RawWeightReading>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<StableWeightReading>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<EdgeCommand>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<EdgeTicketSubmission>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
        builder.Entity<DeviceHeartbeat>()
            .HasQueryFilter(entity =>
                requestContext.TenantId == entity.TenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareTrackedEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareTrackedEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareTrackedEntries()
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            if (entry.Entity is ITenantOwned tenantOwned)
            {
                if (!requestContext.TenantId.HasValue)
                {
                    throw new InvalidOperationException(
                        "A trusted tenant context is required to change tenant-owned data.");
                }

                if (tenantOwned.TenantId != requestContext.TenantId.Value)
                {
                    throw new InvalidOperationException(
                        "The entity tenant does not match the trusted tenant context.");
                }
            }

            if (entry.Entity is not IAuditableEntity)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).CurrentValue = now;
                entry.Property(nameof(IAuditableEntity.CreatedByUserId)).CurrentValue =
                    requestContext.UserId;
            }
            else
            {
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                entry.Property(nameof(IAuditableEntity.CreatedByUserId)).IsModified = false;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(IAuditableEntity.ModifiedAtUtc)).CurrentValue = now;
                entry.Property(nameof(IAuditableEntity.ModifiedByUserId)).CurrentValue =
                    requestContext.UserId;
            }
        }
    }
}
