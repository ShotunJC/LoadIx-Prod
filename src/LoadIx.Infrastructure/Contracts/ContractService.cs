using LoadIx.Application.Abstractions;
using LoadIx.Application.Contracts;
using LoadIx.Application.Security;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Contracts;

public sealed class ContractService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    SecurityAuditWriter auditWriter,
    TimeProvider timeProvider) : IContractService
{
    private const int MaximumPageSize = 100;

    public async Task<ContractResult<ContractPagedResponse<ContractListItem>>> ListAsync(
        ContractQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<ContractPagedResponse<ContractListItem>>(
            LoadIxPermissions.ContractView);
        if (denied is not null) return denied;
        if (query.Page < 1 || query.PageSize is < 1 or > MaximumPageSize
            || query.Direction is not ("asc" or "desc"))
        {
            return Fail<ContractPagedResponse<ContractListItem>>(
                ContractFailure.Validation,
                "contracts.query_invalid",
                "Page must be positive, pageSize must be between 1 and 100, and direction must be asc or desc.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var contracts = dbContext.TransportContracts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            contracts = contracts.Where(contract =>
                contract.ContractNumber.Contains(search)
                || contract.Name.Contains(search));
        }

        if (query.CustomerId.HasValue)
        {
            contracts = contracts.Where(contract =>
                contract.CustomerId == query.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && !string.Equals(query.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<ContractStatus>(query.Status, true, out var status))
            {
                return Fail<ContractPagedResponse<ContractListItem>>(
                    ContractFailure.Validation,
                    "contracts.status_invalid",
                    "Contract status filter is invalid.");
            }

            contracts = status == ContractStatus.Expired
                ? contracts.Where(contract =>
                    contract.EndDate < today
                    && contract.Status != ContractStatus.Archived)
                : contracts.Where(contract =>
                    contract.Status == status
                    && (status != ContractStatus.Active
                        || contract.EndDate == null
                        || contract.EndDate >= today));
        }

        var total = await contracts.CountAsync(cancellationToken);
        var descending = query.Direction == "desc";
        contracts = query.Sort switch
        {
            "name" => descending
                ? contracts.OrderByDescending(contract => contract.Name)
                    .ThenByDescending(contract => contract.Id)
                : contracts.OrderBy(contract => contract.Name)
                    .ThenBy(contract => contract.Id),
            "startDate" => descending
                ? contracts.OrderByDescending(contract => contract.StartDate)
                    .ThenByDescending(contract => contract.Id)
                : contracts.OrderBy(contract => contract.StartDate)
                    .ThenBy(contract => contract.Id),
            "status" => descending
                ? contracts.OrderByDescending(contract => contract.Status)
                    .ThenByDescending(contract => contract.Id)
                : contracts.OrderBy(contract => contract.Status)
                    .ThenBy(contract => contract.Id),
            "contractNumber" => descending
                ? contracts.OrderByDescending(contract => contract.ContractNumber)
                    .ThenByDescending(contract => contract.Id)
                : contracts.OrderBy(contract => contract.ContractNumber)
                    .ThenBy(contract => contract.Id),
            _ => null!,
        };
        if (contracts is null)
        {
            return Fail<ContractPagedResponse<ContractListItem>>(
                ContractFailure.Validation,
                "contracts.sort_invalid",
                "Contract sort field is invalid.");
        }

        var rows = await contracts
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(contract => new
            {
                Contract = contract,
                CustomerName = dbContext.Customers
                    .Where(customer => customer.Id == contract.CustomerId)
                    .Select(customer => customer.TradingName)
                    .Single(),
                RouteCount = contract.Routes.Count,
                CommodityCount = contract.Commodities.Count,
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(row => new ContractListItem(
            row.Contract.Id,
            row.Contract.ContractNumber,
            row.Contract.Name,
            row.Contract.CustomerId,
            row.CustomerName,
            row.Contract.StartDate,
            row.Contract.EndDate,
            EffectiveStatus(row.Contract, today),
            row.Contract.DefaultCurrency,
            row.RouteCount,
            row.CommodityCount,
            Version(row.Contract.RowVersion))).ToArray();
        return ContractResult<ContractPagedResponse<ContractListItem>>.Success(
            new ContractPagedResponse<ContractListItem>(
                items,
                query.Page,
                query.PageSize,
                total,
                (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<ContractResult<ContractDetail>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<ContractDetail>(LoadIxPermissions.ContractView);
        if (denied is not null) return denied;
        var contract = await ContractQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return contract is null
            ? NotFound<ContractDetail>()
            : ContractResult<ContractDetail>.Success(
                await MapAsync(contract, cancellationToken));
    }

    public async Task<ContractResult<ContractDetail>> CreateAsync(
        ContractWriteModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<ContractDetail>(LoadIxPermissions.ContractManage);
        if (denied is not null) return denied;
        var validation = await ValidateReferencesAsync(model, cancellationToken);
        if (validation is not null) return validation;
        var definitions = BuildDefinitions(model, renewIds: false);
        if (definitions.Result is not null) return definitions.Result;
        var result = TransportContract.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.CustomerId,
            model.ContractNumber,
            model.Name,
            model.StartDate,
            model.EndDate,
            model.DefaultCurrency,
            model.PaymentTermsDays,
            model.WeightTolerancePercent,
            model.Notes,
            definitions.Routes!,
            model.CommodityIds,
            definitions.Rates!);
        if (result.IsFailure) return Validation<ContractDetail>(result.Error!);

        dbContext.TransportContracts.Add(result.Value);
        return await SaveAsync(
            result.Value,
            "Created",
            cancellationToken);
    }

    public async Task<ContractResult<ContractDetail>> UpdateAsync(
        Guid id,
        ContractWriteModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<ContractDetail>(LoadIxPermissions.ContractManage);
        if (denied is not null) return denied;
        var contract = await ContractQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null) return NotFound<ContractDetail>();
        if (!ApplyVersion(contract, model.Version)) return InvalidVersion<ContractDetail>();
        var validation = await ValidateReferencesAsync(model, cancellationToken);
        if (validation is not null) return validation;
        var definitions = BuildDefinitions(model, renewIds: true);
        if (definitions.Result is not null) return definitions.Result;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.ContractRates
            .Where(item => item.TransportContractId == contract.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ContractCommodities
            .Where(item => item.TransportContractId == contract.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.ContractRoutes
            .Where(item => item.TransportContractId == contract.Id)
            .ExecuteDeleteAsync(cancellationToken);
        var trackedChildren = contract.Rates.Cast<object>()
            .Concat(contract.Commodities)
            .Concat(contract.Routes)
            .ToArray();
        foreach (var child in trackedChildren)
        {
            dbContext.Entry(child).State = EntityState.Detached;
        }

        var result = contract.Update(
            model.CustomerId,
            model.ContractNumber,
            model.Name,
            model.StartDate,
            model.EndDate,
            model.DefaultCurrency,
            model.PaymentTermsDays,
            model.WeightTolerancePercent,
            model.Notes,
            definitions.Routes!,
            model.CommodityIds,
            definitions.Rates!);
        if (result.IsFailure) return Validation<ContractDetail>(result.Error!);
        dbContext.ChangeTracker.DetectChanges();
        foreach (var child in trackedChildren)
        {
            dbContext.Entry(child).State = EntityState.Detached;
        }

        dbContext.ContractRoutes.AddRange(contract.Routes);
        dbContext.ContractRates.AddRange(contract.Rates);
        dbContext.ContractCommodities.AddRange(contract.Commodities);
        var saved = await SaveAsync(contract, "Updated", cancellationToken);
        if (saved.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return saved;
    }

    public Task<ContractResult<ContractDetail>> ActivateAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken) =>
        TransitionAsync(id, version, "Activated", contract => contract.Activate(),
            cancellationToken);

    public Task<ContractResult<ContractDetail>> SuspendAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken) =>
        TransitionAsync(id, version, "Suspended", contract => contract.Suspend(),
            cancellationToken);

    public async Task<ContractResult<RateCalculationResponse>> PreviewRateAsync(
        Guid id,
        RatePreviewModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<RateCalculationResponse>(LoadIxPermissions.ContractView);
        if (denied is not null) return denied;
        var contract = await dbContext.TransportContracts
            .Include(item => item.Rates)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null) return NotFound<RateCalculationResponse>();
        var rate = contract.Rates.SingleOrDefault(item => item.Id == model.RateId);
        if (rate is null)
        {
            return Fail<RateCalculationResponse>(
                ContractFailure.NotFound,
                "contract.rate_not_found",
                "The requested contract rate was not found.");
        }

        var result = ContractRateCalculator.Calculate(
            rate,
            new RateCalculationInput(
                model.SourceNetTonnes,
                model.DestinationNetTonnes,
                model.ReconciledTonnes,
                model.CalculationDate),
            contract.DefaultCurrency);
        return result.IsFailure
            ? Validation<RateCalculationResponse>(result.Error!)
            : ContractResult<RateCalculationResponse>.Success(
                new RateCalculationResponse(
                    result.Value.Total.Amount,
                    result.Value.Total.CurrencyCode,
                    result.Value.BillableTonnes,
                    result.Value.Breakdown,
                    result.Value.RateSnapshot,
                    result.Value.InputSnapshot));
    }

    public async Task<ContractResult<IReadOnlyList<ContractHistoryItem>>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<IReadOnlyList<ContractHistoryItem>>(
            LoadIxPermissions.ContractView);
        if (denied is not null) return denied;
        if (!await dbContext.TransportContracts.AnyAsync(
            item => item.Id == id, cancellationToken))
        {
            return NotFound<IReadOnlyList<ContractHistoryItem>>();
        }

        var items = await dbContext.SecurityAuditEvents.AsNoTracking()
            .Where(item => item.TargetType == "TransportContract"
                && item.TargetId == id.ToString())
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new ContractHistoryItem(
                item.Id,
                item.EventType,
                item.Outcome,
                item.ActorUserId,
                item.OccurredAtUtc,
                item.IsSupportContext))
            .ToListAsync(cancellationToken);
        return ContractResult<IReadOnlyList<ContractHistoryItem>>.Success(items);
    }

    private async Task<ContractResult<ContractDetail>> TransitionAsync(
        Guid id,
        string version,
        string action,
        Func<TransportContract, Result> transition,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<ContractDetail>(LoadIxPermissions.ContractManage);
        if (denied is not null) return denied;
        var contract = await ContractQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null) return NotFound<ContractDetail>();
        if (!ApplyVersion(contract, version)) return InvalidVersion<ContractDetail>();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (action == "Activated"
            && (contract.StartDate > today || contract.EndDate < today))
        {
            return Fail<ContractDetail>(
                ContractFailure.Validation,
                "contract.activation_dates_invalid",
                "Only a contract effective today can be activated.");
        }

        var result = transition(contract);
        return result.IsFailure
            ? Validation<ContractDetail>(result.Error!)
            : await SaveAsync(contract, action, cancellationToken);
    }

    private async Task<ContractResult<ContractDetail>?> ValidateReferencesAsync(
        ContractWriteModel model,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Customers.AnyAsync(
            item => item.Id == model.CustomerId && item.IsActive, cancellationToken))
        {
            return Fail<ContractDetail>(
                ContractFailure.Validation,
                "contract.customer_invalid",
                "The contract customer must be active in the current tenant.");
        }

        var locationIds = model.Routes
            .SelectMany(route => new[]
            {
                route.SourceLocationId,
                route.DestinationLocationId,
            })
            .Distinct()
            .ToArray();
        if (await dbContext.Locations.CountAsync(
            item => locationIds.Contains(item.Id) && item.IsActive,
            cancellationToken) != locationIds.Length)
        {
            return Fail<ContractDetail>(
                ContractFailure.Validation,
                "contract.locations_invalid",
                "Every contract location must be active in the current tenant.");
        }

        if (await dbContext.Commodities.CountAsync(
            item => model.CommodityIds.Contains(item.Id) && item.IsActive,
            cancellationToken) != model.CommodityIds.Distinct().Count())
        {
            return Fail<ContractDetail>(
                ContractFailure.Validation,
                "contract.commodities_invalid",
                "Every permitted commodity must be active in the current tenant.");
        }

        return null;
    }

    private static (
        IReadOnlyList<ContractRouteDefinition>? Routes,
        IReadOnlyList<ContractRateDefinition>? Rates,
        ContractResult<ContractDetail>? Result) BuildDefinitions(
        ContractWriteModel model,
        bool renewIds)
    {
        if (model.Routes.Any(route =>
            !route.Id.HasValue || route.Id.Value == Guid.Empty))
        {
            return (null, null, Fail<ContractDetail>(
                ContractFailure.Validation,
                "contract.route_id_required",
                "Every route requires a client-generated identifier."));
        }

        var routeIds = model.Routes.Select(route => route.Id!.Value).ToHashSet();
        if (model.Rates.Any(rate => !routeIds.Contains(rate.ContractRouteId)))
        {
            return (null, null, Fail<ContractDetail>(
                ContractFailure.Validation,
                "contract.rate_route_invalid",
                "Every rate must reference a submitted route."));
        }

        var idMap = model.Routes.ToDictionary(
            route => route.Id!.Value,
            route => renewIds ? Guid.NewGuid() : route.Id!.Value);
        var routes = model.Routes.Select(route => new ContractRouteDefinition(
            idMap[route.Id!.Value],
            route.SourceLocationId,
            route.DestinationLocationId,
            route.Reference)).ToArray();
        var rates = new List<ContractRateDefinition>();
        foreach (var rate in model.Rates)
        {
            if (!Enum.TryParse<ContractRateType>(rate.RateType, true, out var type)
                || !Enum.TryParse<MidpointRounding>(
                    rate.RoundingMode, true, out var rounding))
            {
                return (null, null, Fail<ContractDetail>(
                    ContractFailure.Validation,
                    "contract.rate_enum_invalid",
                    "Rate type or rounding mode is invalid."));
            }

            rates.Add(new ContractRateDefinition(
                renewIds ? Guid.NewGuid() : rate.Id,
                idMap[rate.ContractRouteId],
                type,
                rate.EffectiveFrom,
                rate.EffectiveTo,
                rate.FixedAmount,
                rate.BaseFee,
                rate.PerTonneAmount,
                rate.MinimumCharge,
                rate.FuelSurchargePercent,
                rate.RoundingDecimalPlaces,
                rounding));
        }

        return (routes, rates, null);
    }

    private IQueryable<TransportContract> ContractQuery() =>
        dbContext.TransportContracts
            .Include(item => item.Routes)
            .Include(item => item.Rates)
            .Include(item => item.Commodities)
            .AsSplitQuery();

    private async Task<ContractDetail> MapAsync(
        TransportContract contract,
        CancellationToken cancellationToken)
    {
        var customerName = await dbContext.Customers
            .Where(item => item.Id == contract.CustomerId)
            .Select(item => item.TradingName)
            .SingleAsync(cancellationToken);
        var locationIds = contract.Routes.SelectMany(item =>
            new[] { item.SourceLocationId, item.DestinationLocationId }).Distinct();
        var locationNames = await dbContext.Locations
            .Where(item => locationIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var commodityIds = contract.Commodities.Select(item => item.CommodityId);
        var commodities = await dbContext.Commodities
            .Where(item => commodityIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return new ContractDetail(
            contract.Id,
            contract.ContractNumber,
            contract.Name,
            contract.CustomerId,
            customerName,
            contract.StartDate,
            contract.EndDate,
            EffectiveStatus(contract, today),
            contract.DefaultCurrency,
            contract.PaymentTermsDays,
            contract.WeightTolerancePercent,
            contract.Notes,
            contract.Routes.Select(route => new ContractRouteModel(
                route.Id,
                route.Reference,
                route.SourceLocationId,
                locationNames[route.SourceLocationId],
                route.DestinationLocationId,
                locationNames[route.DestinationLocationId])).ToArray(),
            contract.Rates.Select(rate => new ContractRateModel(
                rate.Id,
                rate.ContractRouteId,
                rate.RateType.ToString(),
                rate.EffectiveFrom,
                rate.EffectiveTo,
                rate.FixedAmount,
                rate.BaseFee,
                rate.PerTonneAmount,
                rate.MinimumCharge,
                rate.FuelSurchargePercent,
                rate.RoundingDecimalPlaces,
                rate.RoundingMode.ToString())).ToArray(),
            contract.Commodities.Select(item =>
            {
                var commodity = commodities[item.CommodityId];
                return new ContractCommodityModel(
                    commodity.Id, commodity.Code, commodity.Name);
            }).ToArray(),
            Version(contract.RowVersion));
    }

    private async Task<ContractResult<ContractDetail>> SaveAsync(
        TransportContract contract,
        string action,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditWriter.WriteAsync(
                $"Contract.{action}",
                "Succeeded",
                "TransportContract",
                contract.Id.ToString(),
                requestContext.TenantId,
                requestContext.UserId,
                requestContext.IsSupportContext,
                null,
                cancellationToken);
            return ContractResult<ContractDetail>.Success(
                await MapAsync(contract, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail<ContractDetail>(
                ContractFailure.Conflict,
                "concurrency.conflict",
                "The contract changed after it was loaded. Refresh and try again.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException?.Message.Contains(
                "duplicate", StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains(
                "UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Fail<ContractDetail>(
                ContractFailure.Conflict,
                "contract.duplicate",
                "A contract with the same number or route reference already exists.");
        }
    }

    private ContractResult<T>? Authorize<T>(string permission) =>
        !requestContext.TenantId.HasValue
        || !requestContext.HasPermission(permission)
            ? Fail<T>(
                ContractFailure.Forbidden,
                "authorization.denied",
                "You do not have permission to perform this operation.")
            : null;

    private bool ApplyVersion(TransportContract contract, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try
        {
            dbContext.Entry(contract).Property("RowVersion")
                .OriginalValue = Convert.FromBase64String(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EffectiveStatus(TransportContract contract, DateOnly today) =>
        contract.EndDate < today && contract.Status != ContractStatus.Archived
            ? ContractStatus.Expired.ToString()
            : contract.Status.ToString();

    private static string Version(byte[] value) => Convert.ToBase64String(value);

    private static ContractResult<T> NotFound<T>() =>
        Fail<T>(
            ContractFailure.NotFound,
            "contract.not_found",
            "The requested contract was not found.");

    private static ContractResult<T> InvalidVersion<T>() =>
        Fail<T>(
            ContractFailure.Validation,
            "concurrency.version_required",
            "A valid version is required.");

    private static ContractResult<T> Validation<T>(DomainError error) =>
        Fail<T>(ContractFailure.Validation, error.Code, error.Description);

    private static ContractResult<T> Fail<T>(
        ContractFailure failure,
        string code,
        string message) =>
        ContractResult<T>.Fail(failure, code, message);
}
