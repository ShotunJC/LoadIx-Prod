using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Instructions;
using LoadIx.Application.Security;
using LoadIx.Application.Trips;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Instructions;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Instructions;

public sealed class LoadInstructionService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    SecurityAuditWriter auditWriter,
    TimeProvider timeProvider,
    ITripService tripService) : ILoadInstructionService
{
    private const int MaximumPageSize = 100;

    public async Task<InstructionResult<InstructionPagedResponse<InstructionListItem>>>
        ListAsync(InstructionQuery query, CancellationToken cancellationToken)
    {
        var denied = Authorize<InstructionPagedResponse<InstructionListItem>>(
            LoadIxPermissions.TripView);
        if (denied is not null) return denied;
        if (query.Page < 1 || query.PageSize is < 1 or > MaximumPageSize
            || query.Direction is not ("asc" or "desc"))
        {
            return Fail<InstructionPagedResponse<InstructionListItem>>(
                InstructionFailure.Validation,
                "instructions.query_invalid",
                "Page must be positive, pageSize must be between 1 and 100, and direction must be asc or desc.");
        }

        var instructions = dbContext.LoadInstructions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            instructions = instructions.Where(item =>
                item.InstructionNumber.Contains(search)
                || item.CustomerPurchaseOrderReference.Contains(search)
                || item.CustomerInstructionReference.Contains(search));
        }

        if (query.CustomerId.HasValue)
        {
            instructions = instructions.Where(item =>
                item.CustomerId == query.CustomerId.Value);
        }

        if (query.ContractId.HasValue)
        {
            instructions = instructions.Where(item =>
                item.TransportContractId == query.ContractId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && !string.Equals(query.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<LoadInstructionStatus>(
                query.Status, true, out var status))
            {
                return Fail<InstructionPagedResponse<InstructionListItem>>(
                    InstructionFailure.Validation,
                    "instructions.status_invalid",
                    "Instruction status filter is invalid.");
            }
            instructions = instructions.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority)
            && !string.Equals(query.Priority, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<LoadInstructionPriority>(
                query.Priority, true, out var priority))
            {
                return Fail<InstructionPagedResponse<InstructionListItem>>(
                    InstructionFailure.Validation,
                    "instructions.priority_invalid",
                    "Instruction priority filter is invalid.");
            }
            instructions = instructions.Where(item => item.Priority == priority);
        }

        var total = await instructions.CountAsync(cancellationToken);
        var descending = query.Direction == "desc";
        instructions = query.Sort switch
        {
            "instructionNumber" => descending
                ? instructions.OrderByDescending(item => item.InstructionNumber)
                    .ThenByDescending(item => item.Id)
                : instructions.OrderBy(item => item.InstructionNumber)
                    .ThenBy(item => item.Id),
            "status" => descending
                ? instructions.OrderByDescending(item => item.Status)
                    .ThenByDescending(item => item.Id)
                : instructions.OrderBy(item => item.Status).ThenBy(item => item.Id),
            "priority" => descending
                ? instructions.OrderByDescending(item => item.Priority)
                    .ThenByDescending(item => item.Id)
                : instructions.OrderBy(item => item.Priority).ThenBy(item => item.Id),
            "collectionWindow" => descending
                ? instructions.OrderByDescending(item => item.CollectionWindowStartUtc)
                    .ThenByDescending(item => item.Id)
                : instructions.OrderBy(item => item.CollectionWindowStartUtc)
                    .ThenBy(item => item.Id),
            _ => null!,
        };
        if (instructions is null)
        {
            return Fail<InstructionPagedResponse<InstructionListItem>>(
                InstructionFailure.Validation,
                "instructions.sort_invalid",
                "Instruction sort field is invalid.");
        }

        var rows = await instructions
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new
            {
                Instruction = item,
                CustomerName = dbContext.Customers
                    .Where(customer => customer.Id == item.CustomerId)
                    .Select(customer => customer.TradingName)
                    .Single(),
                ContractNumber = dbContext.TransportContracts
                    .Where(contract => contract.Id == item.TransportContractId)
                    .Select(contract => contract.ContractNumber)
                    .Single(),
                CommodityName = dbContext.Commodities
                    .Where(commodity => commodity.Id == item.CommodityId)
                    .Select(commodity => commodity.Name)
                    .Single(),
                CompletedQuantity = item.Trips
                    .Where(trip => trip.Status == GeneratedTripStatus.Completed)
                    .Sum(trip => trip.CompletedQuantity ?? 0m),
                TripCount = item.Trips.Count,
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(row => new InstructionListItem(
            row.Instruction.Id,
            row.Instruction.InstructionNumber,
            row.Instruction.CustomerId,
            row.CustomerName,
            row.Instruction.TransportContractId,
            row.ContractNumber,
            row.CommodityName,
            row.Instruction.RequestedQuantity,
            row.CompletedQuantity,
            row.Instruction.RequestedQuantity - row.CompletedQuantity,
            row.Instruction.QuantityUnit.ToString(),
            row.Instruction.CollectionWindowStartUtc,
            row.Instruction.Priority.ToString(),
            row.Instruction.Status.ToString(),
            row.TripCount,
            Version(row.Instruction.RowVersion))).ToArray();
        return InstructionResult<InstructionPagedResponse<InstructionListItem>>.Success(
            new InstructionPagedResponse<InstructionListItem>(
                items,
                query.Page,
                query.PageSize,
                total,
                (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<InstructionResult<InstructionDetail>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<InstructionDetail>(LoadIxPermissions.TripView);
        if (denied is not null) return denied;
        var instruction = await InstructionQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return instruction is null
            ? NotFound<InstructionDetail>()
            : InstructionResult<InstructionDetail>.Success(
                await MapAsync(instruction, cancellationToken));
    }

    public async Task<InstructionResult<InstructionDetail>> CreateAsync(
        InstructionWriteModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<InstructionDetail>(LoadIxPermissions.TripManage);
        if (denied is not null) return denied;
        var definition = BuildDefinition(model);
        if (definition.Error is not null) return definition.Error;
        var referenceError = await ValidateContractAsync(
            definition.Value!, cancellationToken);
        if (referenceError is not null) return referenceError;
        var result = LoadInstruction.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            definition.Value!);
        if (result.IsFailure) return Validation<InstructionDetail>(result.Error!);
        dbContext.LoadInstructions.Add(result.Value);
        return await SaveAsync(result.Value, "Created", cancellationToken);
    }

    public async Task<InstructionResult<InstructionDetail>> UpdateAsync(
        Guid id,
        InstructionWriteModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<InstructionDetail>(LoadIxPermissions.TripManage);
        if (denied is not null) return denied;
        var instruction = await InstructionQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (instruction is null) return NotFound<InstructionDetail>();
        if (!ApplyVersion(instruction, model.Version))
            return InvalidVersion<InstructionDetail>();
        var definition = BuildDefinition(model);
        if (definition.Error is not null) return definition.Error;
        var referenceError = await ValidateContractAsync(
            definition.Value!, cancellationToken);
        if (referenceError is not null) return referenceError;
        var result = instruction.Update(definition.Value!);
        return result.IsFailure
            ? Validation<InstructionDetail>(result.Error!)
            : await SaveAsync(instruction, "Updated", cancellationToken);
    }

    public async Task<InstructionResult<InstructionDetail>> PlanAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<InstructionDetail>(LoadIxPermissions.TripManage);
        if (denied is not null) return denied;
        var instruction = await InstructionQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (instruction is null) return NotFound<InstructionDetail>();
        if (!ApplyVersion(instruction, version))
            return InvalidVersion<InstructionDetail>();
        var definition = DefinitionFrom(instruction);
        var referenceError = await ValidateContractAsync(
            definition, cancellationToken);
        if (referenceError is not null) return referenceError;
        var result = instruction.Plan();
        return result.IsFailure
            ? Validation<InstructionDetail>(result.Error!)
            : await SaveAsync(instruction, "Planned", cancellationToken);
    }

    public async Task<InstructionResult<InstructionDetail>> CancelAsync(
        Guid id,
        InstructionCancellationModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<InstructionDetail>(LoadIxPermissions.TripManage);
        if (denied is not null) return denied;
        var instruction = await InstructionQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (instruction is null) return NotFound<InstructionDetail>();
        if (!ApplyVersion(instruction, model.Version))
            return InvalidVersion<InstructionDetail>();
        var result = instruction.Cancel(model.Reason);
        return result.IsFailure
            ? Validation<InstructionDetail>(result.Error!)
            : await SaveAsync(instruction, "Cancelled", cancellationToken);
    }

    public async Task<InstructionResult<InstructionDetail>> GenerateTripsAsync(
        Guid id,
        GenerateTripsModel model,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<InstructionDetail>(LoadIxPermissions.TripManage);
        if (denied is not null) return denied;
        if (!ValidIdempotencyKey(idempotencyKey))
        {
            return Fail<InstructionDetail>(
                InstructionFailure.Validation,
                "idempotency.key_invalid",
                "Idempotency-Key must contain 8 to 128 letters, digits, dots, colons, underscores, or hyphens.");
        }

        var hash = RequestHash(model.Count);
        var existing = await dbContext.TripGenerationOperations.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.LoadInstructionId == id
                && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != hash)
            {
                return Fail<InstructionDetail>(
                    InstructionFailure.Conflict,
                    "idempotency.payload_mismatch",
                    "The idempotency key was already used with a different request.");
            }

            var replay = await InstructionQuery().AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (replay is null) return NotFound<InstructionDetail>();
            var promotionFailure = await PromoteTripsAsync(
                replay, cancellationToken);
            return promotionFailure
                ?? InstructionResult<InstructionDetail>.Success(
                    await MapAsync(replay, cancellationToken));
        }

        var instruction = await InstructionQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (instruction is null) return NotFound<InstructionDetail>();
        if (!ApplyVersion(instruction, model.Version))
            return InvalidVersion<InstructionDetail>();
        var ids = Enumerable.Range(0, Math.Max(model.Count, 0))
            .Select(_ => Guid.NewGuid())
            .ToArray();
        var result = instruction.GenerateTrips(model.Count, ids);
        if (result.IsFailure) return Validation<InstructionDetail>(result.Error!);
        dbContext.GeneratedTrips.AddRange(
            instruction.Trips.Where(trip => ids.Contains(trip.Id)));

        var saved = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(cancellationToken);
                dbContext.TripGenerationOperations.Add(new TripGenerationOperation
                {
                    Id = Guid.NewGuid(),
                    TenantId = requestContext.TenantId!.Value,
                    LoadInstructionId = instruction.Id,
                    IdempotencyKey = idempotencyKey,
                    RequestHash = hash,
                    CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
                });
                var saved = await SaveAsync(
                    instruction, "TripsGenerated", cancellationToken);
                if (saved.IsSuccess)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return saved;
            });
        if (saved.IsSuccess)
        {
            var promotionFailure = await PromoteTripsAsync(
                instruction, cancellationToken);
            if (promotionFailure is not null) return promotionFailure;
        }
        return saved;
    }

    private async Task<InstructionResult<InstructionDetail>?> PromoteTripsAsync(
        LoadInstruction instruction,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var reservation in instruction.Trips.OrderBy(item => item.TripNumber))
        {
            var promoted = await tripService.CreateAsync(
                new CreateTripModel(reservation.Id),
                $"generation:{instruction.Id:N}:{index:000}",
                cancellationToken);
            if (!promoted.IsSuccess)
            {
                return Fail<InstructionDetail>(
                    promoted.Failure == TripFailure.Conflict
                        ? InstructionFailure.Conflict
                        : InstructionFailure.Validation,
                    promoted.Code,
                    promoted.Message);
            }

            index++;
        }

        return null;
    }

    public async Task<InstructionResult<IReadOnlyList<InstructionHistoryItem>>>
        HistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<IReadOnlyList<InstructionHistoryItem>>(
            LoadIxPermissions.TripView);
        if (denied is not null) return denied;
        if (!await dbContext.LoadInstructions.AnyAsync(
            item => item.Id == id, cancellationToken))
        {
            return NotFound<IReadOnlyList<InstructionHistoryItem>>();
        }

        var items = await dbContext.SecurityAuditEvents.AsNoTracking()
            .Where(item => item.TargetType == "LoadInstruction"
                && item.TargetId == id.ToString())
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new InstructionHistoryItem(
                item.Id,
                item.EventType,
                item.Outcome,
                item.ActorUserId,
                item.OccurredAtUtc,
                item.IsSupportContext))
            .ToListAsync(cancellationToken);
        return InstructionResult<IReadOnlyList<InstructionHistoryItem>>.Success(items);
    }

    private async Task<InstructionResult<InstructionDetail>?> ValidateContractAsync(
        LoadInstructionDefinition definition,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.TransportContracts
            .Include(item => item.Routes)
            .Include(item => item.Commodities)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                item => item.Id == definition.TransportContractId,
                cancellationToken);
        if (contract is null
            || contract.CustomerId != definition.CustomerId
            || contract.Status != ContractStatus.Active)
        {
            return Fail<InstructionDetail>(
                InstructionFailure.Validation,
                "instruction.contract_invalid",
                "The instruction requires an active contract for the selected customer.");
        }

        var collectionDate = DateOnly.FromDateTime(
            definition.CollectionWindowStart.UtcDateTime);
        var deliveryDate = DateOnly.FromDateTime(
            definition.DeliveryWindowEnd.UtcDateTime);
        if (collectionDate < contract.StartDate
            || deliveryDate < contract.StartDate
            || contract.EndDate is not null
            && (collectionDate > contract.EndDate || deliveryDate > contract.EndDate))
        {
            return Fail<InstructionDetail>(
                InstructionFailure.Validation,
                "instruction.contract_dates_invalid",
                "Collection and delivery windows must fall within the active contract period.");
        }

        if (!contract.Routes.Any(route => route.Id == definition.ContractRouteId))
        {
            return Fail<InstructionDetail>(
                InstructionFailure.Validation,
                "instruction.route_not_allowed",
                "The selected route is not allowed by the contract.");
        }

        if (!contract.Commodities.Any(item =>
            item.CommodityId == definition.CommodityId))
        {
            return Fail<InstructionDetail>(
                InstructionFailure.Validation,
                "instruction.commodity_not_allowed",
                "The selected commodity is not allowed by the contract.");
        }
        return null;
    }

    private static (
        LoadInstructionDefinition? Value,
        InstructionResult<InstructionDetail>? Error) BuildDefinition(
        InstructionWriteModel model)
    {
        if (!Enum.TryParse<WeightUnit>(model.QuantityUnit, true, out var unit)
            || !Enum.TryParse<LoadInstructionPriority>(
                model.Priority, true, out var priority))
        {
            return (null, Fail<InstructionDetail>(
                InstructionFailure.Validation,
                "instruction.enum_invalid",
                "Quantity unit or priority is invalid."));
        }

        return (new LoadInstructionDefinition(
            model.InstructionNumber,
            model.CustomerId,
            model.TransportContractId,
            model.ContractRouteId,
            model.CommodityId,
            model.RequestedQuantity,
            unit,
            model.RequestedTripCount,
            model.CollectionWindowStart,
            model.CollectionWindowEnd,
            model.DeliveryWindowStart,
            model.DeliveryWindowEnd,
            model.CustomerPurchaseOrderReference,
            model.CustomerInstructionReference,
            priority,
            model.SpecialInstructions), null);
    }

    private static LoadInstructionDefinition DefinitionFrom(
        LoadInstruction instruction) =>
        new(
            instruction.InstructionNumber,
            instruction.CustomerId,
            instruction.TransportContractId,
            instruction.ContractRouteId,
            instruction.CommodityId,
            instruction.RequestedQuantity,
            instruction.QuantityUnit,
            instruction.RequestedTripCount,
            instruction.CollectionWindowStartUtc,
            instruction.CollectionWindowEndUtc,
            instruction.DeliveryWindowStartUtc,
            instruction.DeliveryWindowEndUtc,
            instruction.CustomerPurchaseOrderReference,
            instruction.CustomerInstructionReference,
            instruction.Priority,
            instruction.SpecialInstructions);

    private IQueryable<LoadInstruction> InstructionQuery() =>
        dbContext.LoadInstructions
            .Include(item => item.Trips);

    private async Task<InstructionDetail> MapAsync(
        LoadInstruction instruction,
        CancellationToken cancellationToken)
    {
        var customerName = await dbContext.Customers
            .Where(item => item.Id == instruction.CustomerId)
            .Select(item => item.TradingName)
            .SingleAsync(cancellationToken);
        var contract = await dbContext.TransportContracts
            .Where(item => item.Id == instruction.TransportContractId)
            .Select(item => new { item.ContractNumber })
            .SingleAsync(cancellationToken);
        var route = await dbContext.ContractRoutes
            .Where(item => item.Id == instruction.ContractRouteId)
            .Select(item => new
            {
                item.Reference,
                item.SourceLocationId,
                item.DestinationLocationId,
            })
            .SingleAsync(cancellationToken);
        var locationNames = await dbContext.Locations
            .Where(item => item.Id == route.SourceLocationId
                || item.Id == route.DestinationLocationId)
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var commodity = await dbContext.Commodities
            .Where(item => item.Id == instruction.CommodityId)
            .Select(item => new { item.Code, item.Name })
            .SingleAsync(cancellationToken);
        return new InstructionDetail(
            instruction.Id,
            instruction.InstructionNumber,
            instruction.CustomerId,
            customerName,
            instruction.TransportContractId,
            contract.ContractNumber,
            instruction.ContractRouteId,
            route.Reference,
            locationNames[route.SourceLocationId],
            locationNames[route.DestinationLocationId],
            instruction.CommodityId,
            commodity.Code,
            commodity.Name,
            instruction.RequestedQuantity,
            instruction.CompletedQuantity,
            instruction.RemainingQuantity,
            instruction.OverDeliveredQuantity,
            instruction.UnderDeliveredQuantity,
            instruction.QuantityUnit.ToString(),
            instruction.RequestedTripCount,
            instruction.CollectionWindowStartUtc,
            instruction.CollectionWindowEndUtc,
            instruction.DeliveryWindowStartUtc,
            instruction.DeliveryWindowEndUtc,
            instruction.CustomerPurchaseOrderReference,
            instruction.CustomerInstructionReference,
            instruction.Priority.ToString(),
            instruction.SpecialInstructions,
            instruction.Status.ToString(),
            instruction.CancellationReason,
            instruction.CreatedByUserId,
            instruction.Trips.OrderBy(item => item.TripNumber)
                .Select(item => new GeneratedTripModel(
                    item.Id,
                    item.TripNumber,
                    item.PlannedQuantity,
                    item.CompletedQuantity,
                    item.QuantityUnit.ToString(),
                    item.Status.ToString()))
                .ToArray(),
            Version(instruction.RowVersion));
    }

    private async Task<InstructionResult<InstructionDetail>> SaveAsync(
        LoadInstruction instruction,
        string action,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var value = await MapAsync(instruction, cancellationToken);
            await auditWriter.WriteAsync(
                $"LoadInstruction.{action}",
                "Succeeded",
                "LoadInstruction",
                instruction.Id.ToString(),
                requestContext.TenantId,
                requestContext.UserId,
                requestContext.IsSupportContext,
                null,
                cancellationToken,
                afterSummaryJson: AuditSummary.Serialize(value));
            return InstructionResult<InstructionDetail>.Success(value);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail<InstructionDetail>(
                InstructionFailure.Conflict,
                "concurrency.conflict",
                "The instruction changed after it was loaded. Refresh and try again.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException?.Message.Contains(
                "duplicate", StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains(
                "UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Fail<InstructionDetail>(
                InstructionFailure.Conflict,
                "instruction.duplicate",
                "The instruction number, trip number, or idempotency key already exists.");
        }
    }

    private InstructionResult<T>? Authorize<T>(string permission) =>
        !requestContext.TenantId.HasValue
        || !requestContext.HasPermission(permission)
            ? Fail<T>(
                InstructionFailure.Forbidden,
                "authorization.denied",
                "You do not have permission to perform this operation.")
            : null;

    private bool ApplyVersion(LoadInstruction instruction, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try
        {
            dbContext.Entry(instruction).Property("RowVersion")
                .OriginalValue = Convert.FromBase64String(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool ValidIdempotencyKey(string key) =>
        key.Length is >= 8 and <= 128
        && key.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '.' or ':' or '_' or '-');

    private static string RequestHash(int count) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            count.ToString(CultureInfo.InvariantCulture))));

    private static string Version(byte[] value) => Convert.ToBase64String(value);

    private static InstructionResult<T> NotFound<T>() =>
        Fail<T>(
            InstructionFailure.NotFound,
            "instruction.not_found",
            "The requested load instruction was not found.");

    private static InstructionResult<T> InvalidVersion<T>() =>
        Fail<T>(
            InstructionFailure.Validation,
            "concurrency.version_required",
            "A valid version is required.");

    private static InstructionResult<T> Validation<T>(DomainError error) =>
        Fail<T>(InstructionFailure.Validation, error.Code, error.Description);

    private static InstructionResult<T> Fail<T>(
        InstructionFailure failure,
        string code,
        string message) =>
        InstructionResult<T>.Fail(failure, code, message);
}
