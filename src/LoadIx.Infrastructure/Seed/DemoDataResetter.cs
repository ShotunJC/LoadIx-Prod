using LoadIx.Application.Documents;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Seed;

/// <summary>Deletes only the well-known synthetic demo tenant.</summary>
public sealed class DemoDataResetter(
    LoadIxDbContext db,
    CurrentRequestContext requestContext,
    IObjectStorage objectStorage)
{
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        requestContext.Set(DevelopmentSeedIds.TenantId, DevelopmentSeedIds.ActorId);
        var keys = await db.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ResetWithinTransactionAsync(cancellationToken));

        foreach (var key in keys)
        {
            await objectStorage.DeleteAsync(key, cancellationToken);
        }
    }

    private async Task<string[]> ResetWithinTransactionAsync(
        CancellationToken cancellationToken)
    {
        var keys = await db.Documents.IgnoreQueryFilters()
            .Where(item => item.TenantId == DevelopmentSeedIds.TenantId)
            .Select(item => item.StorageKey)
            .ToArrayAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var tenant = DevelopmentSeedIds.TenantId;

        // The order mirrors FK direction: leaf/history records first, tenant last.
        await DeleteAsync(db.StableWeightReadings, tenant, cancellationToken);
        await DeleteAsync(db.RawWeightReadings, tenant, cancellationToken);
        await DeleteAsync(db.EdgeTicketSubmissions, tenant, cancellationToken);
        await DeleteAsync(db.DeviceHeartbeats, tenant, cancellationToken);
        await DeleteAsync(db.EdgeCommands, tenant, cancellationToken);
        await DeleteAsync(db.WeighbridgeDevices, tenant, cancellationToken);
        await DeleteAsync(db.EdgeDeviceCredentials, tenant, cancellationToken);
        await DeleteAsync(db.EdgeDevices, tenant, cancellationToken);
        await DeleteAsync(db.InvoiceBatchLines, tenant, cancellationToken);
        await DeleteAsync(db.FinancialApprovals, tenant, cancellationToken);
        await DeleteAsync(db.CostEntries, tenant, cancellationToken);
        await DeleteAsync(db.TripFinancials, tenant, cancellationToken);
        await DeleteAsync(db.InvoiceBatches, tenant, cancellationToken);
        await DeleteAsync(db.ReportExportJobs, tenant, cancellationToken);
        await DeleteAsync(db.DocumentUploadOperations, tenant, cancellationToken);
        await DeleteAsync(db.Documents, tenant, cancellationToken);
        await DeleteAsync(db.NotificationDeliveries, tenant, cancellationToken);
        await DeleteAsync(db.NotificationMessages, tenant, cancellationToken);
        await DeleteAsync(db.UserNotificationPreferences, tenant, cancellationToken);
        await DeleteAsync(db.TenantNotificationTemplates, tenant, cancellationToken);
        await DeleteAsync(db.OperationalOutboxMessages, tenant, cancellationToken);
        await DeleteAsync(db.TripLocationUpdates, tenant, cancellationToken);
        await DeleteAsync(db.TripCommandOperations, tenant, cancellationToken);
        await DeleteAsync(db.TripAssignments, tenant, cancellationToken);
        await DeleteAsync(db.TripTransitions, tenant, cancellationToken);
        await DeleteAsync(db.Trips, tenant, cancellationToken);
        await DeleteAsync(db.TripGenerationOperations, tenant, cancellationToken);
        await DeleteAsync(db.GeneratedTrips, tenant, cancellationToken);
        await DeleteAsync(db.LoadInstructions, tenant, cancellationToken);
        await DeleteAsync(db.ContractRates, tenant, cancellationToken);
        await DeleteAsync(db.ContractRoutes, tenant, cancellationToken);
        await DeleteAsync(db.ContractCommodities, tenant, cancellationToken);
        await DeleteAsync(db.TransportContracts, tenant, cancellationToken);
        await DeleteAsync(db.ContactPeople, tenant, cancellationToken);
        await DeleteAsync(db.Locations, tenant, cancellationToken);
        await DeleteAsync(db.Drivers, tenant, cancellationToken);
        await DeleteAsync(db.Vehicles, tenant, cancellationToken);
        await DeleteAsync(db.Trailers, tenant, cancellationToken);
        await DeleteAsync(db.Commodities, tenant, cancellationToken);
        await DeleteAsync(db.Customers, tenant, cancellationToken);
        await DeleteAsync(db.UserProfiles, tenant, cancellationToken);
        await DeleteAsync(db.Branches, tenant, cancellationToken);
        await DeleteAsync(db.TenantRetentionPolicies, tenant, cancellationToken);
        await DeleteAsync(db.AnonymisationRequests, tenant, cancellationToken);
        // Immutable security audit evidence and the stable demo identity are
        // intentionally retained across resets. Resetting operational data must
        // not weaken the audit control or invalidate documented credentials.
        await db.RefreshSessions.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenant).ExecuteDeleteAsync(cancellationToken);
        await db.UserInvitations.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenant).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return keys;
    }

    private static Task<int> DeleteAsync<TEntity>(
        IQueryable<TEntity> source,
        TenantId tenantId,
        CancellationToken cancellationToken)
        where TEntity : class =>
        source.IgnoreQueryFilters().Where(item =>
                EF.Property<TenantId>(item, nameof(ITenantOwned.TenantId)) == tenantId)
            .ExecuteDeleteAsync(cancellationToken);
}
