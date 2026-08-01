using System.Globalization;
using System.Text;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Reporting;
using LoadIx.Domain.Common;
using LoadIx.Domain.Documents;
using LoadIx.Domain.Finance;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Reporting;

public sealed class ReportingService(
    LoadIxDbContext db,
    TimeProvider timeProvider,
    ICurrentRequestContext requestContext) : IReportingService
{
    private const int MaximumRows = 5_000;

    public async Task<DashboardKpis> GetDashboardKpisAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var timeZone = await GetTenantTimeZoneAsync(cancellationToken);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var localStart = new DateTimeOffset(localNow.Date, localNow.Offset);
        var todayStart = localStart.ToUniversalTime();
        var todayEnd = todayStart.AddDays(1);
        var facts = await LoadTripFactsAsync(
            filter with { FromUtc = filter.FromUtc ?? todayStart, ToUtc = filter.ToUtc ?? todayEnd },
            cancellationToken);
        var activeStatuses = new[]
        {
            TripStatus.Planned, TripStatus.Assigned, TripStatus.Dispatched,
            TripStatus.AtCollection, TripStatus.Loading, TripStatus.CollectionComplete,
            TripStatus.InTransit, TripStatus.AtDelivery, TripStatus.Unloading,
            TripStatus.DeliveryComplete, TripStatus.ReconciliationRequired,
        };
        var today = facts.Where(item => item.CompletedAtUtc.HasValue
            && item.CompletedAtUtc.Value >= todayStart
            && item.CompletedAtUtc.Value < todayEnd).ToArray();
        var active = facts.Count(item => activeStatuses.Contains(item.Status));
        var revenue = today.Sum(item => item.Financial?.TaxExclusiveAmount ?? 0m);
        var costs = today.Sum(item => item.Financial?.TotalCost ?? 0m);
        var delivered = today.Sum(item => item.DeliveredTonnes ?? 0m);
        var fleet = await db.Vehicles.AsNoTracking()
            .Where(vehicle => vehicle.IsActive)
            .Select(vehicle => new { vehicle.Id, vehicle.IsAvailable })
            .Take(MaximumRows)
            .ToListAsync(cancellationToken);
        var expiryDate = DateOnly.FromDateTime(localNow.DateTime.Date).AddDays(30);
        var upcoming = await db.Drivers.AsNoTracking().CountAsync(
                driver => driver.LicenceExpiry <= expiryDate && driver.IsActive,
                cancellationToken)
            + await db.Vehicles.AsNoTracking().CountAsync(
                vehicle => (vehicle.LicenceExpiry <= expiryDate
                    || vehicle.RoadworthyExpiry <= expiryDate) && vehicle.IsActive,
                cancellationToken);
        var largestCustomer = today.GroupBy(item => item.Customer)
            .Select(group => group.Sum(item => item.Financial?.TaxExclusiveAmount ?? 0m))
            .DefaultIfEmpty()
            .Max();
        return new DashboardKpis(
            active,
            today.Length,
            decimal.Round(delivered, 3),
            facts.Count(item => item.Status == TripStatus.ReconciliationRequired),
            facts.Count(item => item.Status == TripStatus.ReconciliationRequired && item.IsOutsideTolerance),
            facts.Count(item => item.IsDelayed),
            fleet.Count == 0 ? 0m : decimal.Round(fleet.Count(item => !item.IsAvailable) * 100m / fleet.Count, 2),
            decimal.Round(revenue, 2),
            decimal.Round(revenue - costs, 2),
            revenue == 0 ? 0m : decimal.Round(largestCustomer * 100m / revenue, 2),
            upcoming,
            now,
            timeZone.Id);
    }

    public async Task<ReportingPagedResponse<TripRegisterRow>> GetTripRegisterAsync(
        ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        return Page(facts.OrderByDescending(item => item.PlannedDeliveryAtUtc)
            .ThenBy(item => item.TripNumber)
            .Select(item => new TripRegisterRow(
                item.TripId, item.TripNumber, item.Customer, item.Commodity,
                item.Route, item.Status.ToString(), item.PlannedQuantity,
                item.DeliveredTonnes, item.Driver, item.Vehicle,
                item.PlannedDeliveryAtUtc, item.CompletedAtUtc, item.IsDelayed)), filter);
    }

    public Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByCustomerAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, item => item.Customer, cancellationToken);

    public Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByCommodityAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, item => item.Commodity, cancellationToken);

    public Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByRouteAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, item => item.Route, cancellationToken);

    public async Task<ReportingPagedResponse<VarianceReportRow>> GetVarianceAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        return Page(facts.Where(item => item.SourceNetTonnes.HasValue || item.DestinationNetTonnes.HasValue)
            .OrderBy(item => item.TripNumber)
            .Select(item =>
            {
                var variance = item.SourceNetTonnes.HasValue && item.DestinationNetTonnes.HasValue
                    ? item.DestinationNetTonnes.Value - item.SourceNetTonnes.Value : (decimal?)null;
                var percent = variance.HasValue && item.SourceNetTonnes.GetValueOrDefault() != 0
                    ? variance.Value / item.SourceNetTonnes!.Value * 100m : (decimal?)null;
                return new VarianceReportRow(item.TripNumber, item.Customer,
                    item.SourceNetTonnes, item.DestinationNetTonnes, variance, percent,
                    item.IsOutsideTolerance);
            }), filter);
    }

    public async Task<ReportingPagedResponse<DriverPerformanceRow>> GetDriverPerformanceAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        var rows = facts.Where(item => item.Driver is not null)
            .GroupBy(item => item.Driver!)
            .Select(group => new DriverPerformanceRow(
                group.Key,
                group.Count(item => item.Status == TripStatus.Completed),
                group.Sum(item => item.DeliveredTonnes ?? 0m),
                group.Count(item => item.IsOutsideTolerance),
                OnTimePercent(group)))
            .OrderByDescending(item => item.CompletedTrips).ThenBy(item => item.Driver);
        return Page(rows, filter);
    }

    public async Task<ReportingPagedResponse<VehicleUtilisationRow>> GetVehicleUtilisationAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        var ids = facts.Where(item => item.VehicleId.HasValue).Select(item => item.VehicleId!.Value).Distinct().ToArray();
        var vehicles = await db.Vehicles.AsNoTracking().Where(item => ids.Contains(item.Id)).Take(MaximumRows)
            .Select(item => new { item.Id, item.FleetNumber, item.LicenceExpiry, item.RoadworthyExpiry }).ToListAsync(cancellationToken);
        var rows = vehicles.Select(vehicle =>
        {
            var assigned = facts.Count(item => item.VehicleId == vehicle.Id);
            return new VehicleUtilisationRow(vehicle.FleetNumber,
                assigned, facts.Count(item => item.VehicleId == vehicle.Id && item.Status == TripStatus.Completed),
                assigned == 0 ? 0m : decimal.Round(facts.Count(item => item.VehicleId == vehicle.Id && item.Status == TripStatus.Completed) * 100m / assigned, 2),
                vehicle.LicenceExpiry, vehicle.RoadworthyExpiry);
        }).OrderByDescending(item => item.UtilisationPercent);
        return Page(rows, filter);
    }

    public async Task<ReportingPagedResponse<ContractPerformanceRow>> GetContractPerformanceAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        var rows = facts.GroupBy(item => item.Contract)
            .Select(group => new ContractPerformanceRow(group.Key,
                group.Count(item => item.Status == TripStatus.Completed),
                group.Sum(item => item.DeliveredTonnes ?? 0m),
                group.Sum(item => item.Financial?.TaxExclusiveAmount ?? 0m),
                group.Sum(item => item.Financial?.GrossMargin ?? 0m),
                decimal.Round(group.Count(item => item.IsOutsideTolerance) * 100m / group.Count(), 2)))
            .OrderByDescending(item => item.Revenue).ThenBy(item => item.Contract);
        return Page(rows, filter);
    }

    public Task<ReportingPagedResponse<TonnageReportRow>> GetCustomerRevenueAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, item => item.Customer, cancellationToken, includePlanned: false);

    public async Task<ReportingPagedResponse<TonnageReportRow>> GetTripProfitabilityAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        var rows = facts.Where(item => item.Financial is not null)
            .Select(item => new TonnageReportRow(item.TripNumber, item.DeliveredTonnes ?? 0m, 1,
                item.Financial!.TaxExclusiveAmount, item.Financial.GrossMargin))
            .OrderByDescending(item => item.GrossMargin).ThenBy(item => item.Group);
        return Page(rows, filter);
    }

    public async Task<ReportingPagedResponse<ExceptionReportRow>> GetExceptionsAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        var rows = facts.Where(item => item.IsOutsideTolerance || item.Status == TripStatus.ReconciliationRequired || item.IsDelayed)
            .Select(item => new ExceptionReportRow(item.TripId, item.TripNumber, item.Customer,
                item.Status.ToString(), item.IsOutsideTolerance ? "Outside tolerance" : item.Status == TripStatus.ReconciliationRequired ? "Awaiting reconciliation" : "Delayed",
                item.CompletedAtUtc ?? item.PlannedDeliveryAtUtc));
        return Page(rows.OrderByDescending(item => item.OccurredAtUtc).ThenBy(item => item.TripNumber), filter);
    }

    public async Task<ReportingPagedResponse<PodOutstandingRow>> GetOutstandingPodAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        var completed = facts.Where(item => item.Status == TripStatus.Completed && item.CompletedAtUtc.HasValue).ToArray();
        var ids = completed.Select(item => item.TripId).ToArray();
        var documents = await db.Documents.AsNoTracking().Where(item => ids.Contains(item.OwnerId) && item.OwnerType == DocumentOwnerType.Trip && item.Status == DocumentStatus.Available)
            .GroupBy(item => item.OwnerId).Select(group => new { Id = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var rows = completed.Where(item => !documents.Any(document => document.Id == item.TripId && document.Count > 0))
            .Select(item => new PodOutstandingRow(item.TripId, item.TripNumber, item.Customer, item.Status.ToString(), item.CompletedAtUtc!.Value, 0));
        return Page(rows.OrderBy(item => item.CompletedAtUtc), filter);
    }

    public async Task<ReportingPagedResponse<ExpiryReportRow>> GetExpiriesAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var now = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var end = filter.ToUtc.HasValue ? DateOnly.FromDateTime(filter.ToUtc.Value.UtcDateTime) : now.AddDays(90);
        var drivers = await db.Drivers.AsNoTracking().Where(item => item.IsActive && item.LicenceExpiry <= end).Take(MaximumRows)
            .Select(item => new ExpiryReportRow("Driver", item.FirstName + " " + item.LastName, item.LicenceExpiry, 0, "Licence")).ToListAsync(cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking().Where(item => item.IsActive && (item.LicenceExpiry <= end || item.RoadworthyExpiry <= end)).Take(MaximumRows)
            .SelectMany(item => new[] { new ExpiryReportRow("Vehicle licence", item.FleetNumber, item.LicenceExpiry, 0, "Licence"), new ExpiryReportRow("Vehicle roadworthy", item.FleetNumber, item.RoadworthyExpiry, 0, "Roadworthy") })
            .Where(item => item.ExpiryDate <= end).ToListAsync(cancellationToken);
        var rows = drivers.Concat(vehicles).Select(item => item with { DaysRemaining = item.ExpiryDate.DayNumber - now.DayNumber })
            .OrderBy(item => item.ExpiryDate);
        return Page(rows, filter);
    }

    public async Task<ReportingPagedResponse<InvoiceStatusRow>> GetInvoiceStatusAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var query = db.InvoiceBatches.AsNoTracking();
        if (filter.FromUtc.HasValue) query = query.Where(item => item.CreatedAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(item => item.CreatedAtUtc < filter.ToUtc.Value);
        var rows = await query.GroupBy(item => item.Status).Select(group => new InvoiceStatusRow(group.Key.ToString(), group.Count(), group.Sum(item => item.Total), group.Max(item => (DateTimeOffset?)item.CreatedAtUtc))).OrderBy(item => item.Status).Take(100).ToListAsync(cancellationToken);
        return Page(rows, filter);
    }

    public async Task<byte[]> ExportCsvAsync(string report, ReportingFilter filter, CancellationToken cancellationToken)
    {
        var bounded = filter with { Page = 1, PageSize = MaximumRows };
        var rows = report.ToLowerInvariant() switch
        {
            "trip-register" => await ExportRowsAsync(GetTripRegisterAsync(bounded, cancellationToken)),
            "tonnage-by-customer" => await ExportRowsAsync(GetTonnageByCustomerAsync(bounded, cancellationToken)),
            "tonnage-by-commodity" => await ExportRowsAsync(GetTonnageByCommodityAsync(bounded, cancellationToken)),
            "tonnage-by-route" => await ExportRowsAsync(GetTonnageByRouteAsync(bounded, cancellationToken)),
            "variance" => await ExportRowsAsync(GetVarianceAsync(bounded, cancellationToken)),
            "driver-performance" => await ExportRowsAsync(GetDriverPerformanceAsync(bounded, cancellationToken)),
            "vehicle-utilisation" => await ExportRowsAsync(GetVehicleUtilisationAsync(bounded, cancellationToken)),
            "contract-performance" => await ExportRowsAsync(GetContractPerformanceAsync(bounded, cancellationToken)),
            "customer-revenue" => await ExportRowsAsync(GetCustomerRevenueAsync(bounded, cancellationToken)),
            "trip-profitability" => await ExportRowsAsync(GetTripProfitabilityAsync(bounded, cancellationToken)),
            "exceptions" => await ExportRowsAsync(GetExceptionsAsync(bounded, cancellationToken)),
            "outstanding-pod" => await ExportRowsAsync(GetOutstandingPodAsync(bounded, cancellationToken)),
            "expiries" => await ExportRowsAsync(GetExpiriesAsync(bounded, cancellationToken)),
            "invoice-status" => await ExportRowsAsync(GetInvoiceStatusAsync(bounded, cancellationToken)),
            _ => throw new ArgumentException("Unsupported report export.", nameof(report)),
        };
        var builder = new StringBuilder();
        foreach (var row in rows) builder.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static async Task<IEnumerable<string[]>> ExportRowsAsync<T>(Task<ReportingPagedResponse<T>> responseTask)
    {
        var response = await responseTask;
        var properties = typeof(T).GetProperties();
        return response.Items.Select(item => properties.Select(property =>
        {
            var value = property.GetValue(item);
            return value switch
            {
                null => string.Empty,
                DateTimeOffset dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty,
            };
        }).ToArray());
    }

    private async Task<ReportingPagedResponse<TonnageReportRow>> AggregateAsync(ReportingFilter filter, Func<TripFact, string> key, CancellationToken cancellationToken, bool includePlanned = true)
    {
        var facts = await LoadTripFactsAsync(filter, cancellationToken);
        var rows = facts.Where(item => includePlanned || item.Financial is not null).GroupBy(key)
            .Select(group => new TonnageReportRow(group.Key, group.Sum(item => item.DeliveredTonnes ?? 0m), group.Count(), group.Sum(item => item.Financial?.TaxExclusiveAmount ?? 0m), group.Sum(item => item.Financial?.GrossMargin ?? 0m)))
            .OrderByDescending(item => item.Tonnes).ThenBy(item => item.Group);
        return Page(rows, filter);
    }

    private async Task<List<TripFact>> LoadTripFactsAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var query = db.Trips.AsNoTracking();
        if (filter.FromUtc.HasValue) query = query.Where(item => item.PlannedDeliveryAtUtc >= filter.FromUtc.Value || item.CompletedAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(item => item.PlannedDeliveryAtUtc < filter.ToUtc.Value || item.CompletedAtUtc < filter.ToUtc.Value);
        if (filter.CustomerId.HasValue) query = query.Where(item => item.CustomerId == filter.CustomerId.Value);
        if (filter.BranchId.HasValue) query = query.Where(item => (item.DriverId.HasValue && db.Drivers.Any(driver => driver.Id == item.DriverId && driver.BranchId == filter.BranchId.Value)) || (item.VehicleId.HasValue && db.Vehicles.Any(vehicle => vehicle.Id == item.VehicleId && vehicle.BranchId == filter.BranchId.Value)));
        var trips = await query.OrderByDescending(item => item.PlannedDeliveryAtUtc).ThenBy(item => item.Id).Take(MaximumRows).ToListAsync(cancellationToken);
        var customerIds = trips.Select(item => item.CustomerId).Distinct().ToArray();
        var commodityIds = trips.Select(item => item.CommodityId).Distinct().ToArray();
        var driverIds = trips.Where(item => item.DriverId.HasValue).Select(item => item.DriverId!.Value).Distinct().ToArray();
        var vehicleIds = trips.Where(item => item.VehicleId.HasValue).Select(item => item.VehicleId!.Value).Distinct().ToArray();
        var customers = await db.Customers.AsNoTracking().Where(item => customerIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.TradingName, cancellationToken);
        var commodities = await db.Commodities.AsNoTracking().Where(item => commodityIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var drivers = await db.Drivers.AsNoTracking().Where(item => driverIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking().Where(item => vehicleIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.FleetNumber, cancellationToken);
        var tripIds = trips.Select(item => item.Id).ToArray();
        var financialRows = await db.TripFinancials.AsNoTracking()
            .Where(item => tripIds.Contains(item.TripId))
            .Select(item => new { item.Id, item.TripId, item.TaxExclusiveAmount })
            .ToListAsync(cancellationToken);
        var costTotals = await db.CostEntries.AsNoTracking()
            .Where(item => tripIds.Contains(item.TripFinancialsId))
            .GroupBy(item => item.TripFinancialsId)
            .Select(group => new { TripFinancialsId = group.Key, Total = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.TripFinancialsId, item => item.Total, cancellationToken);
        var financials = financialRows.ToDictionary(
            item => item.TripId,
            item => new FinancialFact(item.TripId, item.TaxExclusiveAmount,
                costTotals.GetValueOrDefault(item.Id),
                decimal.Round(item.TaxExclusiveAmount - costTotals.GetValueOrDefault(item.Id), 4, MidpointRounding.AwayFromZero)));
        var result = new List<TripFact>(trips.Count);
        foreach (var trip in trips)
        {
            var sourceNet = trip.SourceNetWeight;
            var destinationNet = trip.DestinationNetWeight;
            var delivered = destinationNet ?? sourceNet;
            var plannedTonnes = trip.QuantityUnit == WeightUnit.Kilograms ? trip.PlannedQuantity / 1000m : trip.PlannedQuantity;
            var deliveredTonnes = delivered.HasValue && trip.QuantityUnit == WeightUnit.Kilograms ? delivered / 1000m : delivered;
            var outside = deliveredTonnes.HasValue && plannedTonnes > 0 && Math.Abs(deliveredTonnes.Value - plannedTonnes) / plannedTonnes > 0.05m;
            var sourceTonnes = sourceNet.HasValue && trip.QuantityUnit == WeightUnit.Kilograms ? sourceNet / 1000m : sourceNet;
            var destinationTonnes = destinationNet.HasValue && trip.QuantityUnit == WeightUnit.Kilograms ? destinationNet / 1000m : destinationNet;
            result.Add(new TripFact(trip.Id, trip.TripNumber, customers.GetValueOrDefault(trip.CustomerId, "Unknown"), commodities.GetValueOrDefault(trip.CommodityId, "Unknown"), trip.RouteReference, trip.ContractNumber, trip.Status, plannedTonnes, deliveredTonnes, sourceTonnes, destinationTonnes, trip.DriverId.HasValue ? drivers.GetValueOrDefault(trip.DriverId.Value) : null, trip.VehicleId.HasValue ? vehicles.GetValueOrDefault(trip.VehicleId.Value) : null, trip.VehicleId, trip.PlannedDeliveryAtUtc, trip.CompletedAtUtc, trip.PlannedDeliveryAtUtc < timeProvider.GetUtcNow() && trip.Status is not (TripStatus.Completed or TripStatus.Cancelled or TripStatus.Rejected), outside, financials.GetValueOrDefault(trip.Id)));
        }
        return result;
    }

    private async Task<TimeZoneInfo> GetTenantTimeZoneAsync(CancellationToken cancellationToken)
    {
        var value = requestContext.TenantId is { } tenantId
            ? await db.Tenants.AsNoTracking().Where(item => item.Id.Value == tenantId.Value)
                .Select(item => item.TimeZone).SingleOrDefaultAsync(cancellationToken) ?? "UTC"
            : "UTC";
        try { return TimeZoneInfo.FindSystemTimeZoneById(value); } catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }

    private static decimal OnTimePercent(IEnumerable<TripFact> items)
    {
        var completed = items.Where(item => item.Status == TripStatus.Completed).ToArray();
        return completed.Length == 0 ? 0m : decimal.Round(completed.Count(item => item.CompletedAtUtc <= item.PlannedDeliveryAtUtc) * 100m / completed.Length, 2);
    }

    private ReportingPagedResponse<T> Page<T>(IEnumerable<T> source, ReportingFilter filter)
    {
        var items = source.ToList();
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(filter.Page, 1);
        return new ReportingPagedResponse<T>(items.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), page, pageSize, items.Count, items.Count == 0 ? 0 : (int)Math.Ceiling(items.Count / (double)pageSize), timeProvider.GetUtcNow());
    }

    private static string EscapeCsv(string value) => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;

    private sealed record FinancialFact(Guid TripId, decimal TaxExclusiveAmount, decimal TotalCost, decimal GrossMargin);
    private sealed record TripFact(Guid TripId, string TripNumber, string Customer, string Commodity, string Route, string Contract, TripStatus Status, decimal PlannedQuantity, decimal? DeliveredTonnes, decimal? SourceNetTonnes, decimal? DestinationNetTonnes, string? Driver, string? Vehicle, Guid? VehicleId, DateTimeOffset PlannedDeliveryAtUtc, DateTimeOffset? CompletedAtUtc, bool IsDelayed, bool IsOutsideTolerance, FinancialFact? Financial);
}
