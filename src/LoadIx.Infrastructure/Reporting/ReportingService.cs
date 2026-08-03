using System.Linq.Expressions;
using System.Reflection;
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

/// <summary>
/// SQL-backed reporting queries. Date filters are date-only values in the
/// tenant's timezone; every range uses [start, exclusiveEnd).
/// </summary>
public sealed class ReportingService(
    LoadIxDbContext db,
    TimeProvider timeProvider,
    ICurrentRequestContext requestContext) : IReportingService
{
    private const int MaximumPageSize = 100;
    private const int SynchronousExportMaximum = 100;
    private const int ExportBatchSize = 100;

    private static readonly TripStatus[] ActiveStatuses =
    [
        TripStatus.Planned, TripStatus.Assigned, TripStatus.Dispatched,
        TripStatus.AtCollection, TripStatus.Loading, TripStatus.CollectionComplete,
        TripStatus.InTransit, TripStatus.AtDelivery, TripStatus.Unloading,
        TripStatus.DeliveryComplete, TripStatus.ReconciliationRequired,
    ];

    public async Task<DashboardKpis> GetDashboardKpisAsync(
        ReportingFilter filter,
        CancellationToken cancellationToken)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var today = LocalDateRange(DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, settings.TimeZone).DateTime.Date), settings.TimeZone);
        var selected = UtcDateRange(filter, settings.TimeZone);

        // Operational snapshot KPIs deliberately ignore the selected date range:
        // current status, tolerance exceptions and overdue work remain visible.
        var operational = BuildTripFacts(filter, ReportDateMode.None, null);
        var active = await operational.CountAsync(item => ActiveStatuses.Contains(item.Status), cancellationToken);
        var awaiting = await operational.CountAsync(item => item.Status == TripStatus.ReconciliationRequired, cancellationToken);
        // Outside-tolerance is an unresolved operational exception regardless
        // of whether reconciliation has already been submitted.
        var outsideTolerance = await operational.CountAsync(
            item => ActiveStatuses.Contains(item.Status) && item.OutsideTolerance,
            cancellationToken);
        var delayed = await operational.CountAsync(
            item => ActiveStatuses.Contains(item.Status) && item.PlannedDeliveryAtUtc < now,
            cancellationToken);

        // Today means the tenant-local calendar day, not a UTC calendar day.
        var completedToday = BuildTripFacts(filter, ReportDateMode.Completed, today);
        var completedTripsToday = await completedToday.CountAsync(cancellationToken);
        var tonnesDeliveredToday = await completedToday.SumAsync(item => item.DeliveredTonnes ?? 0m, cancellationToken);

        // Revenue, gross-margin amount, and concentration use completed/posted
        // financial facts within the selected period. Gross margin is an amount,
        // not a percentage. Concentration is largest customer revenue / revenue.
        var financial = BuildTripFacts(filter, ReportDateMode.Completed, selected);
        var revenue = await financial.SumAsync(item => item.Revenue, cancellationToken);
        var grossMargin = await financial.SumAsync(item => item.GrossMargin, cancellationToken);
        var largestCustomer = await financial
            .GroupBy(item => item.CustomerId)
            .Select(group => group.Sum(item => item.Revenue))
            .OrderByDescending(value => value)
            .FirstOrDefaultAsync(cancellationToken);

        // Fleet utilisation is the percentage of active tenant vehicles used by
        // at least one selected-period trip, rather than completed/assigned trips.
        var activeFleet = db.Vehicles.AsNoTracking().Where(item => item.IsActive);
        if (filter.BranchId.HasValue) activeFleet = activeFleet.Where(item => item.BranchId == filter.BranchId.Value);
        var fleetTotal = await activeFleet.CountAsync(cancellationToken);
        var usedVehicles = await financial.Where(item => item.VehicleId.HasValue)
            .Select(item => item.VehicleId!.Value).Distinct().CountAsync(cancellationToken);
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, settings.TimeZone).DateTime.Date);
        var expiryDate = localToday.AddDays(30);
        var upcoming = await db.Drivers.AsNoTracking().CountAsync(
                item => item.IsActive && item.LicenceExpiry >= localToday
                    && item.LicenceExpiry <= expiryDate
                    && (!filter.BranchId.HasValue || item.BranchId == filter.BranchId.Value), cancellationToken)
            + await activeFleet.CountAsync(
                item => (item.LicenceExpiry >= localToday && item.LicenceExpiry <= expiryDate)
                    || (item.RoadworthyExpiry >= localToday && item.RoadworthyExpiry <= expiryDate),
                cancellationToken);

        return new DashboardKpis(
            active,
            completedTripsToday,
            decimal.Round(tonnesDeliveredToday, 3),
            awaiting,
            outsideTolerance,
            delayed,
            fleetTotal == 0 ? 0m : decimal.Round(usedVehicles * 100m / fleetTotal, 2),
            decimal.Round(revenue, 2),
            decimal.Round(grossMargin, 2),
            revenue == 0m ? 0m : decimal.Round(largestCustomer * 100m / revenue, 2),
            upcoming,
            now,
            settings.TimeZone.Id,
            settings.CurrencyCode);
    }

    public async Task<ReportingPagedResponse<TripRegisterRow>> GetTripRegisterAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var query = BuildTripFacts(filter, ReportDateMode.PlannedDelivery, await GetRangeAsync(filter, cancellationToken));
        return await PageAsync(query.OrderByDescending(item => item.PlannedDeliveryAtUtc).ThenBy(item => item.TripId)
            .Select(item => new TripRegisterRow(item.TripId, item.TripNumber, item.CustomerName, item.Commodity,
                item.Route, item.Status.ToString(), item.PlannedQuantityTonnes, item.DeliveredTonnes, item.Driver,
                item.Vehicle, item.PlannedDeliveryAtUtc, item.CompletedAtUtc, item.IsDelayed)), filter, cancellationToken);
    }

    public Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByCustomerAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, ReportDateMode.Completed, item => item.CustomerName, cancellationToken);

    public Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByCommodityAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, ReportDateMode.Completed, item => item.Commodity, cancellationToken);

    public Task<ReportingPagedResponse<TonnageReportRow>> GetTonnageByRouteAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, ReportDateMode.Completed, item => item.Route, cancellationToken);

    public async Task<ReportingPagedResponse<VarianceReportRow>> GetVarianceAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var query = BuildTripFacts(filter, ReportDateMode.Completed, await GetRangeAsync(filter, cancellationToken))
            .Where(item => item.SourceNetTonnes.HasValue || item.DestinationNetTonnes.HasValue)
            .Select(item => new VarianceProjection
            {
                TripNumber = item.TripNumber,
                Customer = item.CustomerName,
                Source = item.SourceNetTonnes,
                Destination = item.DestinationNetTonnes,
                Variance = item.SourceNetTonnes.HasValue && item.DestinationNetTonnes.HasValue
                    ? item.DestinationNetTonnes.Value - item.SourceNetTonnes.Value : null,
                VariancePercent = item.SourceNetTonnes.HasValue && item.DestinationNetTonnes.HasValue && item.SourceNetTonnes.Value != 0m
                    ? (item.DestinationNetTonnes.Value - item.SourceNetTonnes.Value) / item.SourceNetTonnes.Value * 100m : null,
                OutsideTolerance = item.OutsideTolerance,
                SortId = item.TripId,
            });
        return await PageAsync(query.OrderBy(item => item.TripNumber).ThenBy(item => item.SortId)
            .Select(item => new VarianceReportRow(item.TripNumber, item.Customer, item.Source, item.Destination,
                item.Variance, item.VariancePercent, item.OutsideTolerance)), filter, cancellationToken);
    }

    public async Task<ReportingPagedResponse<DriverPerformanceRow>> GetDriverPerformanceAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var query = BuildTripFacts(filter, ReportDateMode.Completed, await GetRangeAsync(filter, cancellationToken))
            .Where(item => item.Driver != null)
            .GroupBy(item => item.Driver!)
            .Select(group => new DriverPerformanceProjection
            {
                Driver = group.Key,
                CompletedTrips = group.Count(item => item.Status == TripStatus.Completed),
                DeliveredTonnes = group.Sum(item => item.DeliveredTonnes ?? 0m),
                Exceptions = group.Count(item => item.OutsideTolerance),
                OnTimePercent = group.Count(item => item.Status == TripStatus.Completed) == 0 ? 0m
                    : group.Count(item => item.Status == TripStatus.Completed && item.CompletedAtUtc <= item.PlannedDeliveryAtUtc) * 100m
                        / group.Count(item => item.Status == TripStatus.Completed),
            });
        return await PageAsync(query.OrderByDescending(item => item.CompletedTrips).ThenBy(item => item.Driver)
            .Select(item => new DriverPerformanceRow(item.Driver, item.CompletedTrips, item.DeliveredTonnes,
                item.Exceptions, decimal.Round(item.OnTimePercent, 2))), filter, cancellationToken);
    }

    public async Task<ReportingPagedResponse<VehicleUtilisationRow>> GetVehicleUtilisationAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var query = BuildTripFacts(filter, ReportDateMode.PlannedCollection, await GetRangeAsync(filter, cancellationToken))
            .Where(item => item.VehicleId.HasValue)
            .GroupBy(item => new { item.VehicleId, item.Vehicle, item.LicenceExpiry, item.RoadworthyExpiry })
            .Select(group => new VehicleUtilisationProjection
            {
                Vehicle = group.Key.Vehicle!,
                AssignedTrips = group.Count(),
                CompletedTrips = group.Count(item => item.Status == TripStatus.Completed),
                UtilisationPercent = group.Count() == 0 ? 0m : group.Count(item => item.Status == TripStatus.Completed) * 100m / group.Count(),
                LicenceExpiry = group.Key.LicenceExpiry,
                RoadworthyExpiry = group.Key.RoadworthyExpiry,
            });
        return await PageAsync(query.OrderByDescending(item => item.UtilisationPercent).ThenBy(item => item.Vehicle)
            .Select(item => new VehicleUtilisationRow(item.Vehicle, item.AssignedTrips, item.CompletedTrips,
                decimal.Round(item.UtilisationPercent, 2), item.LicenceExpiry, item.RoadworthyExpiry)), filter, cancellationToken);
    }

    public async Task<ReportingPagedResponse<ContractPerformanceRow>> GetContractPerformanceAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var query = BuildTripFacts(filter, ReportDateMode.Completed, await GetRangeAsync(filter, cancellationToken))
            .GroupBy(item => item.Contract)
            .Select(group => new ContractPerformanceProjection
            {
                Contract = group.Key,
                CompletedTrips = group.Count(item => item.Status == TripStatus.Completed),
                DeliveredTonnes = group.Sum(item => item.DeliveredTonnes ?? 0m),
                Revenue = group.Sum(item => item.Revenue),
                GrossMargin = group.Sum(item => item.GrossMargin),
                ExceptionPercent = group.Count() == 0 ? 0m : group.Count(item => item.OutsideTolerance) * 100m / group.Count(),
            });
        return await PageAsync(query.OrderByDescending(item => item.Revenue).ThenBy(item => item.Contract)
            .Select(item => new ContractPerformanceRow(item.Contract, item.CompletedTrips, item.DeliveredTonnes,
                item.Revenue, item.GrossMargin, decimal.Round(item.ExceptionPercent, 2))), filter, cancellationToken);
    }

    public Task<ReportingPagedResponse<TonnageReportRow>> GetCustomerRevenueAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        AggregateAsync(filter, ReportDateMode.Completed, item => item.CustomerName, cancellationToken, financialOnly: true);

    public async Task<ReportingPagedResponse<TonnageReportRow>> GetTripProfitabilityAsync(ReportingFilter filter, CancellationToken cancellationToken) =>
        await PageAsync(BuildTripFacts(filter, ReportDateMode.Completed, await GetRangeAsync(filter, cancellationToken))
            .Where(item => item.HasFinancials)
            .OrderByDescending(item => item.GrossMargin).ThenBy(item => item.TripId)
            .Select(item => new TonnageReportRow(item.TripNumber, item.DeliveredTonnes ?? 0m, 1, item.Revenue, item.GrossMargin)), filter, cancellationToken);

    public async Task<ReportingPagedResponse<ExceptionReportRow>> GetExceptionsAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var query = BuildTripFacts(filter, ReportDateMode.PlannedDelivery, await GetRangeAsync(filter, cancellationToken))
            .Where(item => item.OutsideTolerance || item.Status == TripStatus.ReconciliationRequired
                || (ActiveStatuses.Contains(item.Status) && item.PlannedDeliveryAtUtc < now))
            .Select(item => new ExceptionReportRow(item.TripId, item.TripNumber, item.CustomerName,
                item.Status.ToString(), item.OutsideTolerance ? "Outside tolerance"
                    : item.Status == TripStatus.ReconciliationRequired ? "Awaiting reconciliation" : "Delayed",
                item.CompletedAtUtc ?? item.PlannedDeliveryAtUtc));
        return await PageAsync(query.OrderByDescending(item => item.OccurredAtUtc).ThenBy(item => item.TripId), filter, cancellationToken);
    }

    public async Task<ReportingPagedResponse<PodOutstandingRow>> GetOutstandingPodAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var range = await GetRangeAsync(filter, cancellationToken);
        var query = BuildTripFacts(filter, ReportDateMode.Completed, range)
            .Where(item => item.Status == TripStatus.Completed && item.CompletedAtUtc.HasValue
                && !db.Documents.Any(document => document.OwnerType == DocumentOwnerType.Trip
                    && document.OwnerId == item.TripId
                    && document.DocumentType == DocumentType.ProofOfDelivery
                    && document.Status == DocumentStatus.Available))
            .Select(item => new PodOutstandingRow(item.TripId, item.TripNumber, item.CustomerName,
                item.Status.ToString(), item.CompletedAtUtc!.Value, 0));
        return await PageAsync(query.OrderBy(item => item.CompletedAtUtc).ThenBy(item => item.TripId), filter, cancellationToken);
    }

    public async Task<ReportingPagedResponse<ExpiryReportRow>> GetExpiriesAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), settings.TimeZone).DateTime.Date);
        if (filter.FromDate.HasValue && filter.ToDate.HasValue
            && filter.ToDate.Value < filter.FromDate.Value)
        {
            throw new ArgumentException("The expiry end date must be on or after the start date.", nameof(filter));
        }

        // With no explicit dates, keep the operational default bounded. A
        // one-sided filter is intentionally open on the missing side so that
        // historical and future expiry searches are not silently truncated.
        var start = filter.FromDate ?? (filter.ToDate.HasValue ? (DateOnly?)null : today);
        var endExclusive = filter.ToDate?.AddDays(1)
            ?? (filter.FromDate.HasValue ? null : today.AddDays(91));
        var drivers = db.Drivers.AsNoTracking()
            .Where(item => item.IsActive
                && (!filter.BranchId.HasValue || item.BranchId == filter.BranchId.Value)
                && (!start.HasValue || item.LicenceExpiry >= start.Value)
                && (!endExclusive.HasValue || item.LicenceExpiry < endExclusive.Value))
            .Select(item => new ExpiryProjection
            {
                ResourceType = "Driver",
                Resource = item.FirstName + " " + item.LastName,
                ExpiryDate = item.LicenceExpiry,
                Status = "Licence",
                SortId = item.Id,
            });
        var vehicleBase = db.Vehicles.AsNoTracking()
            .Where(item => item.IsActive
                && (!filter.BranchId.HasValue || item.BranchId == filter.BranchId.Value));
        var vehicleLicences = vehicleBase
            .Where(item => (!start.HasValue || item.LicenceExpiry >= start.Value)
                && (!endExclusive.HasValue || item.LicenceExpiry < endExclusive.Value))
            .Select(item => new ExpiryProjection
            {
                ResourceType = "Vehicle licence",
                Resource = item.FleetNumber,
                ExpiryDate = item.LicenceExpiry,
                Status = "Licence",
                SortId = item.Id,
            });
        var vehicleRoadworthies = vehicleBase
            .Where(item => (!start.HasValue || item.RoadworthyExpiry >= start.Value)
                && (!endExclusive.HasValue || item.RoadworthyExpiry < endExclusive.Value))
            .Select(item => new ExpiryProjection
            {
                ResourceType = "Vehicle roadworthy",
                Resource = item.FleetNumber,
                ExpiryDate = item.RoadworthyExpiry,
                Status = "Roadworthy",
                SortId = item.Id,
            });
        var query = drivers.Concat(vehicleLicences).Concat(vehicleRoadworthies);
        var page = await PageAsync(query.OrderBy(item => item.ExpiryDate).ThenBy(item => item.SortId), filter, cancellationToken);
        return new ReportingPagedResponse<ExpiryReportRow>(
            page.Items.Select(item => new ExpiryReportRow(item.ResourceType, item.Resource, item.ExpiryDate,
                item.ExpiryDate.DayNumber - today.DayNumber, item.Status)).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages, page.GeneratedAtUtc, page.TimeZone);
    }

    public async Task<ReportingPagedResponse<InvoiceStatusRow>> GetInvoiceStatusAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        var batches = db.InvoiceBatches.AsNoTracking();
        var range = await GetRangeAsync(filter, cancellationToken);
        if (range is { } dateRange)
        {
            if (dateRange.Start.HasValue) batches = batches.Where(item => item.CreatedAtUtc >= dateRange.Start.Value);
            if (dateRange.EndExclusive.HasValue) batches = batches.Where(item => item.CreatedAtUtc < dateRange.EndExclusive.Value);
        }
        if (filter.CustomerId.HasValue) batches = batches.Where(item => item.CustomerId == filter.CustomerId.Value);

        var selectedBranchId = filter.BranchId.GetValueOrDefault();
        // Group lines by batch before aggregating by status. This keeps a mixed
        // branch batch to one row and sums only lines whose trips belong to the
        // selected branch.
        var scopedBatches = from batch in batches
                            join line in db.InvoiceBatchLines.AsNoTracking()
                                on batch.Id equals line.InvoiceBatchId
                            join trip in db.Trips.AsNoTracking()
                                on line.TripId equals trip.Id
                            where !filter.BranchId.HasValue
                                || (trip.DriverId.HasValue
                                    && db.Drivers.Any(driver => driver.Id == trip.DriverId.Value
                                        && driver.BranchId == selectedBranchId))
                                || (trip.VehicleId.HasValue
                                    && db.Vehicles.Any(vehicle => vehicle.Id == trip.VehicleId.Value
                                        && vehicle.BranchId == selectedBranchId))
                            group line by new { batch.Id, batch.Status, batch.CreatedAtUtc } into groupLines
                            select new
                            {
                                groupLines.Key.Status,
                                groupLines.Key.CreatedAtUtc,
                                Total = groupLines.Sum(line => line.Total),
                            };

        var grouped = scopedBatches.GroupBy(item => item.Status)
            .Select(group => new InvoiceStatusRow(group.Key.ToString(), group.Count(),
                group.Sum(item => item.Total), group.Max(item => (DateTimeOffset?)item.CreatedAtUtc)));
        return await PageAsync(grouped.OrderBy(item => item.Status), filter, cancellationToken);
    }

    public async Task<byte[]> ExportCsvAsync(string report, ReportingFilter filter, CancellationToken cancellationToken)
    {
        var batch = await ExportCsvBatchAsync(report, filter with { Page = 1, PageSize = SynchronousExportMaximum + 1 }, true, cancellationToken);
        if (batch.TotalCount > SynchronousExportMaximum)
        {
            throw new ReportExportTooLargeException(SynchronousExportMaximum, batch.TotalCount);
        }

        return batch.Content;
    }

    public async Task<ReportCsvBatch> ExportCsvBatchAsync(string report, ReportingFilter filter, bool includeHeader, CancellationToken cancellationToken)
    {
        var response = await GetReportPageAsync(report, filter, cancellationToken);
        var builder = new StringBuilder();
        var itemType = response.ItemType;
        var properties = itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        if (includeHeader) builder.AppendLine(string.Join(',', properties.Select(item => ReportCsvFormatter.Escape(ReportCsvFormatter.HumanizeHeader(item.Name)))));
        foreach (var item in response.Items)
        {
            builder.AppendLine(string.Join(',', properties.Select(property => ReportCsvFormatter.Escape(ReportCsvFormatter.FormatCell(property.GetValue(item))))));
        }

        return new ReportCsvBatch(Encoding.UTF8.GetBytes(builder.ToString()), response.TotalCount, response.Items.Count);
    }

    private async Task<ReportCsvPage> GetReportPageAsync(string report, ReportingFilter filter, CancellationToken cancellationToken) =>
        report.ToLowerInvariant() switch
        {
            "trip-register" => await ToCsvPage(GetTripRegisterAsync(filter, cancellationToken)),
            "tonnage-by-customer" => await ToCsvPage(GetTonnageByCustomerAsync(filter, cancellationToken)),
            "tonnage-by-commodity" => await ToCsvPage(GetTonnageByCommodityAsync(filter, cancellationToken)),
            "tonnage-by-route" => await ToCsvPage(GetTonnageByRouteAsync(filter, cancellationToken)),
            "variance" => await ToCsvPage(GetVarianceAsync(filter, cancellationToken)),
            "driver-performance" => await ToCsvPage(GetDriverPerformanceAsync(filter, cancellationToken)),
            "vehicle-utilisation" => await ToCsvPage(GetVehicleUtilisationAsync(filter, cancellationToken)),
            "contract-performance" => await ToCsvPage(GetContractPerformanceAsync(filter, cancellationToken)),
            "customer-revenue" => await ToCsvPage(GetCustomerRevenueAsync(filter, cancellationToken)),
            "trip-profitability" => await ToCsvPage(GetTripProfitabilityAsync(filter, cancellationToken)),
            "exceptions" => await ToCsvPage(GetExceptionsAsync(filter, cancellationToken)),
            "outstanding-pod" => await ToCsvPage(GetOutstandingPodAsync(filter, cancellationToken)),
            "expiries" => await ToCsvPage(GetExpiriesAsync(filter, cancellationToken)),
            "invoice-status" => await ToCsvPage(GetInvoiceStatusAsync(filter, cancellationToken)),
            _ => throw new ArgumentException("Unsupported report export.", nameof(report)),
        };

    private static async Task<ReportCsvPage> ToCsvPage<T>(Task<ReportingPagedResponse<T>> task)
    {
        var response = await task;
        return new ReportCsvPage(response.Items.Cast<object>().ToArray(), response.TotalCount, typeof(T));
    }

    private async Task<ReportingPagedResponse<TonnageReportRow>> AggregateAsync(
        ReportingFilter filter,
        ReportDateMode mode,
        Expression<Func<ReportFact, string>> key,
        CancellationToken cancellationToken,
        bool financialOnly = false)
    {
        var query = BuildTripFacts(filter, mode, await GetRangeAsync(filter, cancellationToken));
        if (financialOnly) query = query.Where(item => item.HasFinancials);
        var grouped = query.GroupBy(key).Select(group => new TonnageProjection
        {
            Group = group.Key,
            Tonnes = group.Sum(item => item.DeliveredTonnes ?? 0m),
            TripCount = group.Count(),
            Revenue = group.Sum(item => item.Revenue),
            GrossMargin = group.Sum(item => item.GrossMargin),
        });
        return await PageAsync(grouped.OrderByDescending(item => item.Tonnes).ThenBy(item => item.Group)
            .Select(item => new TonnageReportRow(item.Group, item.Tonnes, item.TripCount, item.Revenue, item.GrossMargin)), filter, cancellationToken);
    }

    private IQueryable<ReportFact> BuildTripFacts(ReportingFilter filter, ReportDateMode dateMode, UtcRange? range)
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var costTotals = db.CostEntries
            .AsNoTracking()
            .GroupBy(cost => cost.TripFinancialsId)
            .Select(group => new
            {
                TripFinancialsId = group.Key,
                Total = group.Sum(cost => cost.Amount),
            });
        var query =
            from trip in db.Trips.AsNoTracking()
            join customer in db.Customers.AsNoTracking() on trip.CustomerId equals customer.Id
            join commodity in db.Commodities.AsNoTracking() on trip.CommodityId equals commodity.Id
            join contract in db.TransportContracts.AsNoTracking() on trip.TransportContractId equals contract.Id
            join driver in db.Drivers.AsNoTracking() on trip.DriverId equals (Guid?)driver.Id into driverJoin
            from driver in driverJoin.DefaultIfEmpty()
            join vehicle in db.Vehicles.AsNoTracking() on trip.VehicleId equals (Guid?)vehicle.Id into vehicleJoin
            from vehicle in vehicleJoin.DefaultIfEmpty()
            join financial in db.TripFinancials.AsNoTracking() on trip.Id equals financial.TripId into financialJoin
            from financial in financialJoin.DefaultIfEmpty()
            join costTotalCandidate in costTotals
                on financial == null ? Guid.Empty : financial.Id
                equals costTotalCandidate.TripFinancialsId into costJoin
            from costTotal in costJoin.DefaultIfEmpty()
            select new ReportFact
            {
                TripId = trip.Id,
                TripNumber = trip.TripNumber,
                CustomerId = trip.CustomerId,
                CustomerName = customer.TradingName,
                Commodity = commodity.Name,
                Route = trip.RouteReference,
                Contract = trip.ContractNumber,
                Status = trip.Status,
                PlannedQuantityTonnes = trip.QuantityUnit == WeightUnit.Kilograms ? trip.PlannedQuantity / 1000m : trip.PlannedQuantity,
                SourceNetTonnes = trip.SourceGrossWeight.HasValue && trip.SourceTareWeight.HasValue
                    ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault()) / 1000m : trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault()) : null,
                DestinationNetTonnes = trip.DestinationGrossWeight.HasValue && trip.DestinationTareWeight.HasValue
                    ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault()) / 1000m : trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault()) : null,
                Driver = driver == null ? null : driver.FirstName + " " + driver.LastName,
                DriverId = trip.DriverId,
                Vehicle = vehicle == null ? null : vehicle.FleetNumber,
                VehicleId = trip.VehicleId,
                LicenceExpiry = vehicle == null ? null : (DateOnly?)vehicle.LicenceExpiry,
                RoadworthyExpiry = vehicle == null ? null : (DateOnly?)vehicle.RoadworthyExpiry,
                PlannedCollectionAtUtc = trip.PlannedCollectionAtUtc,
                PlannedDeliveryAtUtc = trip.PlannedDeliveryAtUtc,
                CompletedAtUtc = trip.CompletedAtUtc,
                Revenue = financial == null ? 0m : financial.TaxExclusiveAmount,
                TotalCost = costTotal == null ? 0m : costTotal.Total,
                HasFinancials = financial != null,
                TolerancePercent = contract.WeightTolerancePercent,
                DeliveredTonnes = trip.DestinationGrossWeight.HasValue && trip.DestinationTareWeight.HasValue
                    ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault()) / 1000m : trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault())
                    : trip.SourceGrossWeight.HasValue && trip.SourceTareWeight.HasValue
                        ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault()) / 1000m : trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault())
                        : null,
                GrossMargin = financial == null ? 0m : financial.TaxExclusiveAmount
                    - (costTotal == null ? 0m : costTotal.Total),
                OutsideTolerance = trip.PlannedQuantity > 0m
                    && ((trip.DestinationGrossWeight.HasValue && trip.DestinationTareWeight.HasValue)
                        || (trip.SourceGrossWeight.HasValue && trip.SourceTareWeight.HasValue))
                    && ((trip.DestinationGrossWeight.HasValue && trip.DestinationTareWeight.HasValue
                            ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault()) / 1000m : trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault())
                            : trip.SourceGrossWeight.HasValue && trip.SourceTareWeight.HasValue
                                ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault()) / 1000m : trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault())
                                : 0m)
                        > (trip.QuantityUnit == WeightUnit.Kilograms ? trip.PlannedQuantity / 1000m : trip.PlannedQuantity)
                            + (trip.QuantityUnit == WeightUnit.Kilograms ? trip.PlannedQuantity / 1000m : trip.PlannedQuantity) * contract.WeightTolerancePercent / 100m
                        || (trip.DestinationGrossWeight.HasValue && trip.DestinationTareWeight.HasValue
                            ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault()) / 1000m : trip.DestinationGrossWeight.GetValueOrDefault() - trip.DestinationTareWeight.GetValueOrDefault())
                            : trip.SourceGrossWeight.HasValue && trip.SourceTareWeight.HasValue
                                ? (trip.QuantityUnit == WeightUnit.Kilograms ? (trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault()) / 1000m : trip.SourceGrossWeight.GetValueOrDefault() - trip.SourceTareWeight.GetValueOrDefault())
                                : 0m)
                        < (trip.QuantityUnit == WeightUnit.Kilograms ? trip.PlannedQuantity / 1000m : trip.PlannedQuantity)
                            - (trip.QuantityUnit == WeightUnit.Kilograms ? trip.PlannedQuantity / 1000m : trip.PlannedQuantity) * contract.WeightTolerancePercent / 100m),
                IsDelayed = ActiveStatuses.Contains(trip.Status) && trip.PlannedDeliveryAtUtc < now,
            };

        if (filter.CustomerId.HasValue) query = query.Where(item => item.CustomerId == filter.CustomerId.Value);
        if (filter.BranchId.HasValue)
        {
            query = query.Where(item => (item.VehicleId.HasValue && db.Vehicles.Any(vehicle => vehicle.Id == item.VehicleId && vehicle.BranchId == filter.BranchId.Value))
                || (item.DriverId.HasValue && db.Drivers.Any(driver => driver.Id == item.DriverId && driver.BranchId == filter.BranchId.Value)));
        }

        if (dateMode == ReportDateMode.Completed)
        {
            query = query.Where(item => item.CompletedAtUtc.HasValue);
            if (range is { } completedRange)
            {
                query = query.Where(item =>
                    (!completedRange.Start.HasValue || item.CompletedAtUtc >= completedRange.Start.Value)
                    && (!completedRange.EndExclusive.HasValue || item.CompletedAtUtc < completedRange.EndExclusive.Value));
            }
        }
        else if (dateMode != ReportDateMode.None && range is { } dateRange)
        {
            query = dateMode switch
            {
                ReportDateMode.PlannedCollection => query.Where(item => (!dateRange.Start.HasValue || item.PlannedCollectionAtUtc >= dateRange.Start.Value) && (!dateRange.EndExclusive.HasValue || item.PlannedCollectionAtUtc < dateRange.EndExclusive.Value)),
                _ => query.Where(item => (!dateRange.Start.HasValue || item.PlannedDeliveryAtUtc >= dateRange.Start.Value) && (!dateRange.EndExclusive.HasValue || item.PlannedDeliveryAtUtc < dateRange.EndExclusive.Value)),
            };
        }

        return query;
    }

    private async Task<ReportingPagedResponse<T>> PageAsync<T>(IQueryable<T> query, ReportingFilter filter, CancellationToken cancellationToken)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaximumPageSize + 1);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new ReportingPagedResponse<T>(items, page, pageSize, totalCount, totalPages,
            timeProvider.GetUtcNow().ToUniversalTime(), settings.TimeZone.Id);
    }

    private async Task<UtcRange?> GetRangeAsync(ReportingFilter filter, CancellationToken cancellationToken)
    {
        if (!filter.FromDate.HasValue && !filter.ToDate.HasValue) return null;
        var settings = await GetTenantSettingsAsync(cancellationToken);
        return UtcDateRange(filter, settings.TimeZone);
    }

    private static UtcRange? UtcDateRange(ReportingFilter filter, TimeZoneInfo timeZone)
    {
        if (!filter.FromDate.HasValue && !filter.ToDate.HasValue) return null;
        if (filter.FromDate.HasValue && filter.ToDate.HasValue
            && filter.ToDate.Value < filter.FromDate.Value)
        {
            throw new ArgumentException("The report end date must be on or after the start date.", nameof(filter));
        }

        var start = filter.FromDate.HasValue
            ? ReportingDateRange.Resolve(filter.FromDate.Value, filter.FromDate.Value, timeZone).StartUtc
            : (DateTimeOffset?)null;
        var end = filter.ToDate.HasValue
            ? ReportingDateRange.Resolve(filter.ToDate.Value, filter.ToDate.Value, timeZone).ExclusiveEndUtc
            : (DateTimeOffset?)null;
        return new UtcRange(start, end);
    }

    private static UtcRange LocalDateRange(DateOnly date, TimeZoneInfo timeZone)
    {
        var range = ReportingDateRange.Resolve(date, date, timeZone);
        return new UtcRange(range.StartUtc, range.ExclusiveEndUtc);
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = requestContext.TenantId is { } tenantId
            ? await db.Tenants.AsNoTracking().Where(item => item.Id == tenantId)
                .Select(item => new { item.TimeZone, item.DefaultCurrency }).SingleOrDefaultAsync(cancellationToken)
            : null;
        if (settings is null) return new TenantSettings(TimeZoneInfo.Utc, "ZAR");
        try
        {
            return new TenantSettings(TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone), settings.DefaultCurrency);
        }
        catch (TimeZoneNotFoundException) { return new TenantSettings(TimeZoneInfo.Utc, settings.DefaultCurrency); }
        catch (InvalidTimeZoneException) { return new TenantSettings(TimeZoneInfo.Utc, settings.DefaultCurrency); }
    }

    private enum ReportDateMode { None, PlannedDelivery, PlannedCollection, Completed }

    private sealed record TenantSettings(TimeZoneInfo TimeZone, string CurrencyCode);
    private sealed record UtcRange(DateTimeOffset? Start, DateTimeOffset? EndExclusive);
    private sealed record ReportCsvPage(IReadOnlyList<object> Items, int TotalCount, Type ItemType);
    private sealed class ReportFact
    {
        public Guid TripId { get; set; }
        public string TripNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Commodity { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Contract { get; set; } = string.Empty;
        public TripStatus Status { get; set; }
        public decimal PlannedQuantityTonnes { get; set; }
        public decimal? SourceNetTonnes { get; set; }
        public decimal? DestinationNetTonnes { get; set; }
        public decimal? DeliveredTonnes { get; set; }
        public string? Driver { get; set; }
        public Guid? DriverId { get; set; }
        public string? Vehicle { get; set; }
        public Guid? VehicleId { get; set; }
        public DateOnly? LicenceExpiry { get; set; }
        public DateOnly? RoadworthyExpiry { get; set; }
        public DateTimeOffset PlannedCollectionAtUtc { get; set; }
        public DateTimeOffset PlannedDeliveryAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public decimal Revenue { get; set; }
        public decimal TotalCost { get; set; }
        public bool HasFinancials { get; set; }
        public decimal TolerancePercent { get; set; }
        public decimal GrossMargin { get; set; }
        public bool OutsideTolerance { get; set; }
        public bool IsDelayed { get; set; }
    }

    private sealed class TonnageProjection { public string Group { get; set; } = string.Empty; public decimal Tonnes { get; set; } public int TripCount { get; set; } public decimal Revenue { get; set; } public decimal GrossMargin { get; set; } }
    private sealed class VarianceProjection { public string TripNumber { get; set; } = string.Empty; public string Customer { get; set; } = string.Empty; public decimal? Source { get; set; } public decimal? Destination { get; set; } public decimal? Variance { get; set; } public decimal? VariancePercent { get; set; } public bool OutsideTolerance { get; set; } public Guid SortId { get; set; } }
    private sealed class DriverPerformanceProjection { public string Driver { get; set; } = string.Empty; public int CompletedTrips { get; set; } public decimal DeliveredTonnes { get; set; } public int Exceptions { get; set; } public decimal OnTimePercent { get; set; } }
    private sealed class VehicleUtilisationProjection { public string Vehicle { get; set; } = string.Empty; public int AssignedTrips { get; set; } public int CompletedTrips { get; set; } public decimal UtilisationPercent { get; set; } public DateOnly? LicenceExpiry { get; set; } public DateOnly? RoadworthyExpiry { get; set; } }
    private sealed class ContractPerformanceProjection { public string Contract { get; set; } = string.Empty; public int CompletedTrips { get; set; } public decimal DeliveredTonnes { get; set; } public decimal Revenue { get; set; } public decimal GrossMargin { get; set; } public decimal ExceptionPercent { get; set; } }
    private sealed class ExpiryProjection { public string ResourceType { get; set; } = string.Empty; public string Resource { get; set; } = string.Empty; public DateOnly ExpiryDate { get; set; } public string Status { get; set; } = string.Empty; public Guid SortId { get; set; } }
}

public sealed class ReportExportTooLargeException(int maximumRows, int totalRows) : Exception
{
    public int MaximumRows { get; } = maximumRows;
    public int TotalRows { get; } = totalRows;
}
