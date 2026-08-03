using System.Globalization;
using System.Text;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Finance;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Finance;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Finance;

public sealed class FinanceService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    SecurityAuditWriter auditWriter,
    TimeProvider timeProvider) : IFinanceService
{
    private const int MaximumPageSize = 100;

    public async Task<FinanceResult<TenantTaxConfigurationModel>> GetTaxConfigurationAsync(CancellationToken cancellationToken)
    {
        var denied = Authorize<TenantTaxConfigurationModel>(LoadIxPermissions.FinanceView);
        if (denied is not null) return denied;
        var config = await dbContext.TenantTaxConfigurations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return FinanceResult<TenantTaxConfigurationModel>.Success(config is null
            ? new TenantTaxConfigurationModel(null, TaxTreatment.NotReviewed.ToString(), 0m, false, null, null, null)
            : Map(config));
    }

    public async Task<FinanceResult<TenantTaxConfigurationModel>> UpdateTaxConfigurationAsync(TenantTaxConfigurationWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<TenantTaxConfigurationModel>(LoadIxPermissions.FinanceManage);
        if (denied is not null) return denied;
        if (!Enum.TryParse<TaxTreatment>(model.DefaultTaxTreatment, true, out var treatment))
            return Validation<TenantTaxConfigurationModel>(new DomainError("finance.tax_invalid", "Tax treatment is invalid."));
        var timestamp = timeProvider.GetUtcNow().ToUniversalTime();
        var config = await dbContext.TenantTaxConfigurations.SingleOrDefaultAsync(cancellationToken);
        if (config is null)
        {
            var created = TenantTaxConfiguration.Create(Guid.NewGuid(), requestContext.TenantId!.Value, treatment, model.TaxRatePercent, requestContext.UserId!.Value, timestamp);
            if (created.IsFailure) return Validation<TenantTaxConfigurationModel>(created.Error!);
            dbContext.TenantTaxConfigurations.Add(created.Value);
            config = created.Value;
        }
        else
        {
            if (!ApplyVersion(config, model.Version)) return InvalidVersion<TenantTaxConfigurationModel>();
            var updated = config.Update(treatment, model.TaxRatePercent, requestContext.UserId!.Value, timestamp);
            if (updated.IsFailure) return Validation<TenantTaxConfigurationModel>(updated.Error!);
        }

        return await SaveAndReturnAsync(config, "TaxConfiguration.Reviewed", "TenantTaxConfiguration", config.Id, Map, cancellationToken);
    }

    public async Task<FinanceResult<TripFinancialsDetail>> GetTripFinancialsAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var denied = Authorize<TripFinancialsDetail>(LoadIxPermissions.FinanceView);
        if (denied is not null) return denied;
        var financials = await FinancialsQuery().AsNoTracking().SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        return financials is null
            ? await RecalculateTripFinancialsAsync(tripId, cancellationToken)
            : FinanceResult<TripFinancialsDetail>.Success(Map(financials));
    }

    public async Task<FinanceResult<TripFinancialsDetail>> RecalculateTripFinancialsAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var denied = Authorize<TripFinancialsDetail>(LoadIxPermissions.FinanceManage);
        if (denied is not null) return denied;
        var existing = await FinancialsQuery().SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (existing is not null)
        {
            return existing.ApprovalStatus == FinancialApprovalStatus.Approved || existing.IsInvoiced
                ? Fail<TripFinancialsDetail>(FinanceFailure.Conflict, "finance.financials_locked", "Approved or invoiced financial records cannot be silently recalculated.")
                : FinanceResult<TripFinancialsDetail>.Success(Map(existing));
        }

        var built = await BuildFinancialsAsync(tripId, cancellationToken);
        if (built.Result is not null) return built.Result;
        var financials = built.Financials!;
        dbContext.TripFinancials.Add(financials);
        return await SaveAndReturnAsync(financials, "TripFinancials.Calculated", "TripFinancials", financials.Id, Map, cancellationToken);
    }

    public async Task<FinanceResult<TripFinancialsDetail>> AddCostAsync(Guid tripId, CostEntryWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<TripFinancialsDetail>(LoadIxPermissions.FinanceManage);
        if (denied is not null) return denied;
        var financials = await EnsureFinancialsAsync(tripId, cancellationToken);
        if (!financials.IsSuccess) return FinanceResult<TripFinancialsDetail>.Fail(financials.Failure, financials.Code, financials.Message);
        if (!ApplyVersion(financials.Value!, model.Version)) return InvalidVersion<TripFinancialsDetail>();
        if (!Enum.TryParse<CostEntryType>(model.Type, true, out var type))
            return Validation<TripFinancialsDetail>(new DomainError("finance.cost_type_invalid", "Cost type is invalid."));
        var entry = CostEntry.Create(Guid.NewGuid(), requestContext.TenantId!.Value, financials.Value!.Id, type, model.Amount, financials.Value.CurrencyCode, model.Description, requestContext.UserId!.Value, timeProvider.GetUtcNow().ToUniversalTime(), model.IsAdjustment);
        if (entry.IsFailure) return Validation<TripFinancialsDetail>(entry.Error!);
        var added = model.IsAdjustment ? financials.Value.AddAdjustment(entry.Value) : financials.Value.AddCost(entry.Value);
        if (added.IsFailure) return Validation<TripFinancialsDetail>(added.Error!);
        dbContext.Entry(financials.Value).State = EntityState.Detached;
        return await SaveAndReturnAsync(financials.Value, model.IsAdjustment ? "Cost.Adjusted" : "Cost.Added", "TripFinancials", financials.Value.Id, Map, cancellationToken);
    }

    public async Task<FinanceResult<TripFinancialsDetail>> ApproveTripFinancialsAsync(Guid tripId, ApproveFinancialsModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<TripFinancialsDetail>(LoadIxPermissions.FinanceManage);
        if (denied is not null) return denied;
        var financials = await EnsureFinancialsAsync(tripId, cancellationToken);
        if (!financials.IsSuccess) return FinanceResult<TripFinancialsDetail>.Fail(financials.Failure, financials.Code, financials.Message);
        if (!ApplyVersion(financials.Value!, model.Version)) return InvalidVersion<TripFinancialsDetail>();
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var approved = financials.Value!.Approve(requestContext.UserId!.Value, now, model.Reason);
        if (approved.IsFailure) return Validation<TripFinancialsDetail>(approved.Error!);
        AddApproval("TripFinancials", financials.Value.Id, "Approved", now, model.Reason);
        return await SaveAndReturnAsync(
            financials.Value,
            "TripFinancials.Approved",
            "TripFinancials",
            financials.Value.Id,
            Map,
            cancellationToken,
            model.Reason);
    }

    public async Task<FinanceResult<FinancePagedResponse<InvoiceReadyTrip>>> ListInvoiceReadyAsync(InvoiceReadyQuery query, CancellationToken cancellationToken)
    {
        var denied = Authorize<FinancePagedResponse<InvoiceReadyTrip>>(LoadIxPermissions.FinanceView);
        if (denied is not null) return denied;
        if (query.Page < 1 || query.PageSize is < 1 or > MaximumPageSize || query.Direction is not ("asc" or "desc"))
            return Fail<FinancePagedResponse<InvoiceReadyTrip>>(FinanceFailure.Validation, "finance.query_invalid", "Page must be positive, pageSize must be between 1 and 100, and direction must be asc or desc.");
        var rows = dbContext.TripFinancials.AsNoTracking()
            .Where(item => item.ApprovalStatus == FinancialApprovalStatus.Approved && item.InvoiceBatchId == null)
            .Join(dbContext.Trips.AsNoTracking().Where(trip => trip.Status == TripStatus.Completed), financials => financials.TripId, trip => trip.Id, (financials, trip) => new { Financials = financials, Trip = trip });
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(item => item.Financials.TripNumber.Contains(search) || item.Financials.CustomerName.Contains(search) || item.Financials.ContractNumber.Contains(search));
        }
        if (query.CustomerId.HasValue) rows = rows.Where(item => item.Financials.CustomerId == query.CustomerId);
        if (query.ContractId.HasValue) rows = rows.Where(item => item.Financials.TransportContractId == query.ContractId);
        var total = await rows.CountAsync(cancellationToken);
        var descending = query.Direction == "desc";
        rows = query.Sort switch
        {
            "customer" => descending ? rows.OrderByDescending(item => item.Financials.CustomerName).ThenByDescending(item => item.Financials.Id) : rows.OrderBy(item => item.Financials.CustomerName).ThenBy(item => item.Financials.Id),
            "total" => descending ? rows.OrderByDescending(item => item.Financials.Total).ThenByDescending(item => item.Financials.Id) : rows.OrderBy(item => item.Financials.Total).ThenBy(item => item.Financials.Id),
            "completed" => descending ? rows.OrderByDescending(item => item.Trip.CompletedAtUtc).ThenByDescending(item => item.Financials.Id) : rows.OrderBy(item => item.Trip.CompletedAtUtc).ThenBy(item => item.Financials.Id),
            _ => null!,
        };
        if (rows is null) return Fail<FinancePagedResponse<InvoiceReadyTrip>>(FinanceFailure.Validation, "finance.sort_invalid", "Sort field is invalid.");
        var items = await rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new InvoiceReadyTrip(item.Financials.Id, item.Financials.TripId, item.Financials.TripNumber, item.Financials.CustomerId, item.Financials.CustomerName, item.Financials.TransportContractId, item.Financials.ContractNumber, item.Financials.BillableTonnes, item.Financials.TaxExclusiveAmount, item.Financials.TaxAmount, item.Financials.Total, item.Financials.CurrencyCode, item.Trip.CompletedAtUtc!.Value))
            .ToListAsync(cancellationToken);
        return FinanceResult<FinancePagedResponse<InvoiceReadyTrip>>.Success(new FinancePagedResponse<InvoiceReadyTrip>(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<FinanceResult<IReadOnlyList<InvoiceBatchPreview>>> PreviewInvoiceBatchAsync(InvoiceBatchPreviewRequest model, CancellationToken cancellationToken)
    {
        var denied = Authorize<IReadOnlyList<InvoiceBatchPreview>>(LoadIxPermissions.FinanceView);
        if (denied is not null) return denied;
        var financials = await InvoiceableFinancials<IReadOnlyList<InvoiceBatchPreview>>(model.FinancialsIds, cancellationToken);
        if (financials.Result is not null) return financials.Result;
        return FinanceResult<IReadOnlyList<InvoiceBatchPreview>>.Success(financials.Items!.GroupBy(item => new { item.CustomerId, item.CustomerName, item.TransportContractId, item.ContractNumber, item.CurrencyCode }).Select(group => new InvoiceBatchPreview(group.Key.CustomerId, group.Key.CustomerName, group.Key.TransportContractId, group.Key.ContractNumber, group.Key.CurrencyCode, group.Sum(item => item.TaxExclusiveAmount), group.Sum(item => item.TaxAmount), group.Sum(item => item.Total), group.Select(MapPreviewLine).ToArray())).ToArray());
    }

    public async Task<FinanceResult<InvoiceBatchDetail>> CreateInvoiceBatchAsync(InvoiceBatchCreateModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<InvoiceBatchDetail>(LoadIxPermissions.FinanceManage);
        if (denied is not null) return denied;
        var financials = await InvoiceableFinancials<InvoiceBatchDetail>(model.FinancialsIds, cancellationToken);
        if (financials.Result is not null) return financials.Result;
        var items = financials.Items!;
        var groups = items.GroupBy(item => new { item.CustomerId, item.TransportContractId, item.CurrencyCode }).ToArray();
        if (groups.Length != 1) return Fail<InvoiceBatchDetail>(FinanceFailure.Validation, "invoice.group_invalid", "Create one invoice batch per customer, contract, and currency.");
        var first = items[0];
        var batch = InvoiceBatch.Create(Guid.NewGuid(), requestContext.TenantId!.Value, await NextBatchNumberAsync(cancellationToken), first.CustomerId, first.CustomerName, first.TransportContractId, first.ContractNumber, first.CurrencyCode, timeProvider.GetUtcNow().ToUniversalTime());
        if (batch.IsFailure) return Validation<InvoiceBatchDetail>(batch.Error!);
        foreach (var item in items)
        {
            var line = InvoiceBatchLine.Create(Guid.NewGuid(), batch.Value.Id, item);
            if (line.IsFailure) return Validation<InvoiceBatchDetail>(line.Error!);
            var added = batch.Value.AddLine(line.Value);
            if (added.IsFailure) return Validation<InvoiceBatchDetail>(added.Error!);
            var attached = item.AttachInvoiceBatch(batch.Value.Id);
            if (attached.IsFailure) return Validation<InvoiceBatchDetail>(attached.Error!);
        }
        dbContext.InvoiceBatches.Add(batch.Value);
        return await SaveAndReturnAsync(batch.Value, "InvoiceBatch.Created", "InvoiceBatch", batch.Value.Id, Map, cancellationToken);
    }

    public async Task<FinanceResult<FinancePagedResponse<InvoiceBatchListItem>>> ListInvoiceBatchesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var denied = Authorize<FinancePagedResponse<InvoiceBatchListItem>>(LoadIxPermissions.FinanceView);
        if (denied is not null) return denied;
        if (page < 1 || pageSize is < 1 or > MaximumPageSize) return Fail<FinancePagedResponse<InvoiceBatchListItem>>(FinanceFailure.Validation, "finance.query_invalid", "Page must be positive and pageSize must be between 1 and 100.");
        var query = dbContext.InvoiceBatches.AsNoTracking().Include(item => item.Lines).OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(item => MapListItem(item)).ToListAsync(cancellationToken);
        return FinanceResult<FinancePagedResponse<InvoiceBatchListItem>>.Success(new FinancePagedResponse<InvoiceBatchListItem>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    public async Task<FinanceResult<InvoiceBatchDetail>> GetInvoiceBatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<InvoiceBatchDetail>(LoadIxPermissions.FinanceView);
        if (denied is not null) return denied;
        var batch = await BatchQuery().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return batch is null ? NotFound<InvoiceBatchDetail>("invoice.not_found", "The requested invoice batch was not found.") : FinanceResult<InvoiceBatchDetail>.Success(Map(batch));
    }

    public Task<FinanceResult<InvoiceBatchDetail>> ApproveInvoiceBatchAsync(Guid id, VersionedFinanceCommand model, CancellationToken cancellationToken) =>
        BatchTransitionAsync(id, model.Version, "InvoiceBatch.Approved", (batch, now) => batch.Approve(requestContext.UserId!.Value, now), model.Reason, cancellationToken);

    public Task<FinanceResult<InvoiceBatchDetail>> MarkInvoiceBatchExportedAsync(Guid id, VersionedFinanceCommand model, CancellationToken cancellationToken) =>
        BatchTransitionAsync(id, model.Version, "InvoiceBatch.Exported", (batch, now) => batch.MarkExported(requestContext.UserId!.Value, now), model.Reason, cancellationToken);

    public async Task<FinanceResult<byte[]>> ExportInvoiceBatchCsvAsync(Guid id, CancellationToken cancellationToken)
    {
        var batch = await GetInvoiceBatchAsync(id, cancellationToken);
        if (!batch.IsSuccess) return FinanceResult<byte[]>.Fail(batch.Failure, batch.Code, batch.Message);
        var builder = new StringBuilder();
        builder.AppendLine("BatchNumber,Customer,Contract,TripNumber,BillableTonnes,TaxExclusiveAmount,TaxTreatment,TaxAmount,Total,Currency");
        foreach (var line in batch.Value!.Lines)
            builder.AppendLine(string.Join(',', Escape(batch.Value.BatchNumber), Escape(batch.Value.CustomerName), Escape(batch.Value.ContractNumber), Escape(line.TripNumber), line.BillableTonnes.ToString(CultureInfo.InvariantCulture), line.TaxExclusiveAmount.ToString(CultureInfo.InvariantCulture), Escape(line.TaxTreatment), line.TaxAmount.ToString(CultureInfo.InvariantCulture), line.Total.ToString(CultureInfo.InvariantCulture), Escape(batch.Value.CurrencyCode)));
        var content = Encoding.UTF8.GetBytes(builder.ToString());
        await auditWriter.WriteAsync(
            "Finance.Export",
            "Succeeded",
            "InvoiceBatch",
            id.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                format = "csv",
                byteCount = content.Length,
            }));
        return FinanceResult<byte[]>.Success(content);
    }

    public async Task<FinanceResult<byte[]>> ExportInvoiceBatchPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        var batch = await GetInvoiceBatchAsync(id, cancellationToken);
        if (!batch.IsSuccess)
        {
            return FinanceResult<byte[]>.Fail(batch.Failure, batch.Code, batch.Message);
        }

        var content = SimplePdf(batch.Value!);
        await auditWriter.WriteAsync(
            "Finance.Export",
            "Succeeded",
            "InvoiceBatch",
            id.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                format = "pdf",
                byteCount = content.Length,
            }));
        return FinanceResult<byte[]>.Success(content);
    }

    private async Task<FinanceResult<InvoiceBatchDetail>> BatchTransitionAsync(Guid id, string version, string action, Func<InvoiceBatch, DateTimeOffset, Result> transition, string? reason, CancellationToken cancellationToken)
    {
        var denied = Authorize<InvoiceBatchDetail>(LoadIxPermissions.FinanceManage);
        if (denied is not null) return denied;
        var batch = await BatchQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null) return NotFound<InvoiceBatchDetail>("invoice.not_found", "The requested invoice batch was not found.");
        if (!ApplyVersion(batch, version)) return InvalidVersion<InvoiceBatchDetail>();
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var result = transition(batch, now);
        if (result.IsFailure) return Validation<InvoiceBatchDetail>(result.Error!);
        AddApproval("InvoiceBatch", batch.Id, action.Split('.').Last(), now, reason);
        return await SaveAndReturnAsync(batch, action, "InvoiceBatch", batch.Id, Map, cancellationToken, reason);
    }

    private async Task<FinanceResult<TripFinancials>> EnsureFinancialsAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var financials = await FinancialsQuery().SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (financials is not null) return FinanceResult<TripFinancials>.Success(financials);
        var built = await BuildFinancialsAsync(tripId, cancellationToken);
        if (built.Result is not null) return FinanceResult<TripFinancials>.Fail(built.Result.Failure, built.Result.Code, built.Result.Message);
        var created = built.Financials!;
        dbContext.TripFinancials.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);
        return FinanceResult<TripFinancials>.Success(created);
    }

    private async Task<(TripFinancials? Financials, FinanceResult<TripFinancialsDetail>? Result)> BuildFinancialsAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tripId, cancellationToken);
        if (trip is null) return (null, NotFound<TripFinancialsDetail>("trip.not_found", "The requested trip was not found."));
        if (trip.Status != TripStatus.Completed || !trip.IsReconciled || !trip.HasRequiredDocuments)
            return (null, Fail<TripFinancialsDetail>(FinanceFailure.Validation, "finance.trip_not_ready", "Only completed, reconciled trips with required documents can be financially prepared."));
        var tax = await dbContext.TenantTaxConfigurations.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (tax is null || !tax.IsReviewed)
            return (null, Fail<TripFinancialsDetail>(FinanceFailure.Validation, "finance.tax_not_reviewed", "Tenant tax configuration must be explicitly reviewed before calculation."));
        var customerName = await dbContext.Customers.AsNoTracking().Where(item => item.Id == trip.CustomerId).Select(item => item.TradingName).SingleAsync(cancellationToken);
        var billableTonnes = trip.DestinationNetWeight ?? trip.SourceNetWeight ?? trip.PlannedQuantity;
        var created = TripFinancials.Create(Guid.NewGuid(), trip.TenantId, trip.Id, trip.TripNumber, trip.CustomerId, customerName, trip.TransportContractId, trip.ContractNumber, trip.RateSnapshot, billableTonnes, tax.DefaultTaxTreatment, tax.TaxRatePercent, timeProvider.GetUtcNow().ToUniversalTime());
        return created.IsFailure ? (null, Validation<TripFinancialsDetail>(created.Error!)) : (created.Value, null);
    }

    private async Task<(IReadOnlyList<TripFinancials>? Items, FinanceResult<T>? Result)> InvoiceableFinancials<T>(IReadOnlyList<Guid> financialsIds, CancellationToken cancellationToken)
    {
        var ids = financialsIds.Distinct().ToArray();
        if (ids.Length == 0) return (null, Fail<T>(FinanceFailure.Validation, "invoice.selection_required", "Select at least one invoiceable trip."));
        var items = await FinancialsQuery().Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        if (items.Count != ids.Length || items.Any(item => item.ApprovalStatus != FinancialApprovalStatus.Approved || item.InvoiceBatchId != null))
            return (null, Fail<T>(FinanceFailure.Conflict, "invoice.selection_invalid", "One or more selected trips are no longer invoiceable."));
        return (items, null);
    }

    private IQueryable<TripFinancials> FinancialsQuery() => dbContext.TripFinancials.Include(item => item.Costs).AsSplitQuery();
    private IQueryable<InvoiceBatch> BatchQuery() => dbContext.InvoiceBatches.Include(item => item.Lines).AsSplitQuery();

    private static TripFinancialsDetail Map(TripFinancials item) => new(item.Id, item.TripId, item.TripNumber, item.CustomerId, item.CustomerName, item.TransportContractId, item.ContractNumber, item.CurrencyCode, item.RateType.ToString(), item.BillableTonnes, item.Subtotal, item.Surcharge, item.TaxExclusiveAmount, item.TaxTreatment.ToString(), item.TaxRatePercent, item.TaxAmount, item.Total, item.CalculatedAtUtc, item.CalculationVersion, item.ApprovalStatus.ToString(), item.InvoiceBatchId, new FinancialSummary(new MoneyAmount(item.TaxExclusiveAmount, item.CurrencyCode), new MoneyAmount(item.TotalCost, item.CurrencyCode), new MoneyAmount(item.GrossMargin, item.CurrencyCode), item.MarginPercentage, item.CostPerTonne, item.RevenuePerTonne), item.Costs.OrderBy(cost => cost.OccurredAtUtc).ThenBy(cost => cost.Id).Select(cost => new CostEntryModel(cost.Id, cost.Type.ToString(), cost.Amount, cost.CurrencyCode, cost.Description, cost.IsAdjustment, cost.ActorUserId, cost.OccurredAtUtc)).ToArray(), Version(item.RowVersion));
    private static TenantTaxConfigurationModel Map(TenantTaxConfiguration item) => new(item.Id, item.DefaultTaxTreatment.ToString(), item.TaxRatePercent, item.IsReviewed, item.ReviewedByUserId, item.ReviewedAtUtc, Version(item.RowVersion));
    private static InvoiceBatchDetail Map(InvoiceBatch item) => new(item.Id, item.BatchNumber, item.CustomerId, item.CustomerName, item.TransportContractId, item.ContractNumber, item.CurrencyCode, item.Status.ToString(), item.TaxExclusiveAmount, item.TaxAmount, item.Total, item.CreatedAtUtc, item.ApprovedAtUtc, item.ApprovedByUserId, item.ExportedAtUtc, item.ExportedByUserId, item.Lines.OrderBy(line => line.TripNumber).Select(MapLine).ToArray(), Version(item.RowVersion));
    private static InvoiceBatchListItem MapListItem(InvoiceBatch item) => new(item.Id, item.BatchNumber, item.CustomerId, item.CustomerName, item.ContractNumber, item.Status.ToString(), item.TaxExclusiveAmount, item.TaxAmount, item.Total, item.CurrencyCode, item.CreatedAtUtc, item.ApprovedAtUtc, item.ExportedAtUtc, Version(item.RowVersion));
    private static InvoiceBatchLineModel MapLine(InvoiceBatchLine line) => new(line.Id, line.TripFinancialsId, line.TripId, line.TripNumber, line.BillableTonnes, line.TaxExclusiveAmount, line.TaxTreatment.ToString(), line.TaxAmount, line.Total);
    private static InvoiceBatchLineModel MapPreviewLine(TripFinancials item) => new(Guid.Empty, item.Id, item.TripId, item.TripNumber, item.BillableTonnes, item.TaxExclusiveAmount, item.TaxTreatment.ToString(), item.TaxAmount, item.Total);

    private async Task<string> NextBatchNumberAsync(CancellationToken cancellationToken)
    {
        var today = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var count = await dbContext.InvoiceBatches.CountAsync(cancellationToken) + 1;
        return $"INV-{today}-{count:0000}";
    }

    private void AddApproval(string targetType, Guid targetId, string outcome, DateTimeOffset timestamp, string? reason)
    {
        var approval = FinancialApproval.Create(Guid.NewGuid(), requestContext.TenantId!.Value, targetType, targetId, outcome, requestContext.UserId!.Value, timestamp, reason);
        if (approval.IsSuccess) dbContext.FinancialApprovals.Add(approval.Value);
    }

    private async Task<FinanceResult<TModel>> SaveAndReturnAsync<TEntity, TModel>(TEntity entity, string eventType, string targetType, Guid targetId, Func<TEntity, TModel> map, CancellationToken cancellationToken, string? reason = null)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var value = map(entity);
            await auditWriter.WriteAsync(
                $"Finance.{eventType}",
                "Succeeded",
                targetType,
                targetId.ToString(),
                requestContext.TenantId,
                requestContext.UserId,
                requestContext.IsSupportContext,
                null,
                cancellationToken,
                afterSummaryJson: AuditSummary.Serialize(value),
                reason: reason);
            return FinanceResult<TModel>.Success(value);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail<TModel>(FinanceFailure.Conflict, "concurrency.conflict", "The financial record changed after it was loaded.");
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true || exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Fail<TModel>(FinanceFailure.Conflict, "finance.duplicate", "The financial operation conflicts with an existing record.");
        }
    }

    private FinanceResult<T>? Authorize<T>(string permission) => !requestContext.TenantId.HasValue || !requestContext.UserId.HasValue || !requestContext.HasPermission(permission) ? Fail<T>(FinanceFailure.Forbidden, "authorization.denied", "You do not have permission to perform this operation.") : null;

    private bool ApplyVersion(object entity, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try
        {
            dbContext.Entry(entity).Property("RowVersion").OriginalValue = Convert.FromBase64String(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] SimplePdf(InvoiceBatchDetail batch)
    {
        var text = $"LoadIx invoice batch {batch.BatchNumber}\\nCustomer: {batch.CustomerName}\\nTotal: {batch.Total.ToString(CultureInfo.InvariantCulture)} {batch.CurrencyCode}";
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
        var stream = $"BT /F1 12 Tf 72 720 Td ({escaped}) Tj ET";
        var pdf = $"""
%PDF-1.4
1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj
4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj
5 0 obj << /Length {stream.Length} >> stream
{stream}
endstream endobj
xref
0 6
0000000000 65535 f 
trailer << /Root 1 0 R /Size 6 >>
startxref
0
%%EOF
""";
        return Encoding.ASCII.GetBytes(pdf);
    }

    private static string Escape(string value) => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal) ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    private static string Version(byte[] value) => Convert.ToBase64String(value);
    private static FinanceResult<T> NotFound<T>(string code, string message) => Fail<T>(FinanceFailure.NotFound, code, message);
    private static FinanceResult<T> InvalidVersion<T>() => Fail<T>(FinanceFailure.Validation, "concurrency.version_required", "A valid version is required.");
    private static FinanceResult<T> Validation<T>(DomainError error) => Fail<T>(FinanceFailure.Validation, error.Code, error.Description);
    private static FinanceResult<T> Fail<T>(FinanceFailure failure, string code, string message) => FinanceResult<T>.Fail(failure, code, message);
}
