using LoadIx.Application.MasterData;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Fleet;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.MasterData;

public sealed partial class MasterDataService
{
    public async Task<MasterDataResult<PagedResponse<DriverItem>>> ListDriversAsync(
        MasterDataQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<PagedResponse<DriverItem>>(LoadIxPermissions.FleetView);
        var invalid = ValidateQuery<DriverItem>(query);
        if (denied is not null) return denied;
        if (invalid is not null) return invalid;
        var source = dbContext.Drivers.AsNoTracking();
        source = query.Status switch
        {
            "active" => source.Where(item => item.IsActive),
            "inactive" => source.Where(item => !item.IsActive),
            _ => source,
        };
        if (query.BranchId.HasValue)
            source = source.Where(item => item.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item =>
                item.FirstName.Contains(search)
                || item.LastName.Contains(search)
                || item.LicenceNumber.Contains(search));
        }

        var descending = query.Direction == "desc";
        source = (query.Sort?.ToLowerInvariant(), descending) switch
        {
            ("reference", true) => source.OrderByDescending(item => item.LicenceNumber)
                .ThenByDescending(item => item.Id),
            ("reference", false) => source.OrderBy(item => item.LicenceNumber)
                .ThenBy(item => item.Id),
            ("expiry", true) => source.OrderByDescending(item => item.LicenceExpiry)
                .ThenByDescending(item => item.Id),
            ("expiry", false) => source.OrderBy(item => item.LicenceExpiry)
                .ThenBy(item => item.Id),
            (_, true) => source.OrderByDescending(item => item.LastName)
                .ThenByDescending(item => item.FirstName)
                .ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.LastName)
                .ThenBy(item => item.FirstName)
                .ThenBy(item => item.Id),
        };
        var total = await source.CountAsync(cancellationToken);
        var entities = await source.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var branches = await BranchNamesAsync(
            entities.Select(item => item.BranchId), cancellationToken);
        return MasterDataResult<PagedResponse<DriverItem>>.Success(
            Page(
                entities.Select(item => MapDriver(item, branches[item.BranchId])).ToArray(),
                query,
                total));
    }

    public async Task<MasterDataResult<DriverItem>> GetDriverAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<DriverItem>(LoadIxPermissions.FleetView);
        if (denied is not null) return denied;
        var entity = await dbContext.Drivers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<DriverItem>("Driver");
        var branchName = await BranchNameAsync(entity.BranchId, cancellationToken);
        return MasterDataResult<DriverItem>.Success(MapDriver(entity, branchName!));
    }

    public async Task<MasterDataResult<DriverItem>> CreateDriverAsync(
        DriverWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<DriverItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var branchName = await BranchNameAsync(model.BranchId, cancellationToken);
        if (branchName is null) return InvalidBranch<DriverItem>();
        if (!Enum.TryParse<DriverType>(model.DriverType, true, out var type))
            return MasterDataResult<DriverItem>.Fail(
                MasterDataFailure.Validation,
                "driver.invalid_type",
                "Driver type must be Employee or Contractor.");
        var result = Driver.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.BranchId,
            model.FirstName,
            model.LastName,
            type,
            model.IdentificationReference,
            model.MobileNumber,
            model.LicenceNumber,
            model.LicenceExpiry,
            model.EmergencyContactName,
            model.EmergencyContactNumber);
        if (result.IsFailure) return Validation<DriverItem>(result.Error!);
        dbContext.Drivers.Add(result.Value);
        return await SaveAsync(
            () => MapDriver(result.Value, branchName),
            "Created",
            "Driver",
            result.Value.Id,
            cancellationToken);
    }

    public async Task<MasterDataResult<DriverItem>> UpdateDriverAsync(
        Guid id, DriverWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<DriverItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Drivers.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<DriverItem>("Driver");
        if (!TryApplyVersion(dbContext, entity, model.Version))
            return InvalidVersion<DriverItem>();
        var branchName = await BranchNameAsync(model.BranchId, cancellationToken);
        if (branchName is null) return InvalidBranch<DriverItem>();
        if (!Enum.TryParse<DriverType>(model.DriverType, true, out var type))
            return MasterDataResult<DriverItem>.Fail(
                MasterDataFailure.Validation,
                "driver.invalid_type",
                "Driver type must be Employee or Contractor.");
        var result = entity.Update(
            model.BranchId,
            model.FirstName,
            model.LastName,
            type,
            model.IdentificationReference,
            model.MobileNumber,
            model.LicenceNumber,
            model.LicenceExpiry,
            model.EmergencyContactName,
            model.EmergencyContactNumber);
        if (result.IsFailure) return Validation<DriverItem>(result.Error!);
        return await SaveAsync(
            () => MapDriver(entity, branchName),
            "Updated",
            "Driver",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<DriverItem>> SetDriverActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken)
    {
        var denied = Authorize<DriverItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Drivers.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<DriverItem>("Driver");
        if (!TryApplyVersion(dbContext, entity, version))
            return InvalidVersion<DriverItem>();
        if (active) entity.Activate(); else entity.Deactivate();
        var branchName = await BranchNameAsync(entity.BranchId, cancellationToken);
        return await SaveAsync(
            () => MapDriver(entity, branchName!),
            active ? "Activated" : "Deactivated",
            "Driver",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<PagedResponse<VehicleItem>>> ListVehiclesAsync(
        MasterDataQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<PagedResponse<VehicleItem>>(LoadIxPermissions.FleetView);
        var invalid = ValidateQuery<VehicleItem>(query);
        if (denied is not null) return denied;
        if (invalid is not null) return invalid;
        var source = dbContext.Vehicles.AsNoTracking();
        source = query.Status switch
        {
            "active" => source.Where(item => item.IsActive),
            "inactive" => source.Where(item => !item.IsActive),
            _ => source,
        };
        if (query.BranchId.HasValue)
            source = source.Where(item => item.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item =>
                item.Registration.Contains(search)
                || item.FleetNumber.Contains(search)
                || item.Make.Contains(search)
                || item.Model.Contains(search));
        }

        source = OrderVehicles(source, query);
        var total = await source.CountAsync(cancellationToken);
        var entities = await source.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var branches = await BranchNamesAsync(
            entities.Select(item => item.BranchId), cancellationToken);
        return MasterDataResult<PagedResponse<VehicleItem>>.Success(
            Page(
                entities.Select(item => MapVehicle(item, branches[item.BranchId])).ToArray(),
                query,
                total));
    }

    public async Task<MasterDataResult<VehicleItem>> GetVehicleAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<VehicleItem>(LoadIxPermissions.FleetView);
        if (denied is not null) return denied;
        var entity = await dbContext.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<VehicleItem>("Vehicle");
        var branchName = await BranchNameAsync(entity.BranchId, cancellationToken);
        return MasterDataResult<VehicleItem>.Success(MapVehicle(entity, branchName!));
    }

    public async Task<MasterDataResult<VehicleItem>> CreateVehicleAsync(
        VehicleWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<VehicleItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var values = await ValidateVehicleWriteAsync(model, cancellationToken);
        if (!values.IsSuccess) return values.Result!;
        var result = Vehicle.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.BranchId,
            model.Registration,
            model.FleetNumber,
            model.Make,
            model.Model,
            model.Year,
            values.Tare!,
            values.Capacity!,
            model.RoadworthyExpiry,
            model.LicenceExpiry);
        if (result.IsFailure) return Validation<VehicleItem>(result.Error!);
        result.Value.SetAvailability(model.IsAvailable);
        dbContext.Vehicles.Add(result.Value);
        return await SaveAsync(
            () => MapVehicle(result.Value, values.BranchName!),
            "Created",
            "Vehicle",
            result.Value.Id,
            cancellationToken);
    }

    public async Task<MasterDataResult<VehicleItem>> UpdateVehicleAsync(
        Guid id, VehicleWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<VehicleItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Vehicles.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<VehicleItem>("Vehicle");
        if (!TryApplyVersion(dbContext, entity, model.Version))
            return InvalidVersion<VehicleItem>();
        var values = await ValidateVehicleWriteAsync(model, cancellationToken);
        if (!values.IsSuccess) return values.Result!;
        var result = entity.Update(
            model.BranchId,
            model.Registration,
            model.FleetNumber,
            model.Make,
            model.Model,
            model.Year,
            values.Tare!,
            values.Capacity!,
            model.RoadworthyExpiry,
            model.LicenceExpiry,
            model.IsAvailable);
        if (result.IsFailure) return Validation<VehicleItem>(result.Error!);
        return await SaveAsync(
            () => MapVehicle(entity, values.BranchName!),
            "Updated",
            "Vehicle",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<VehicleItem>> SetVehicleActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken)
    {
        var denied = Authorize<VehicleItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Vehicles.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<VehicleItem>("Vehicle");
        if (!TryApplyVersion(dbContext, entity, version))
            return InvalidVersion<VehicleItem>();
        if (active) entity.Activate(); else entity.Deactivate();
        var branchName = await BranchNameAsync(entity.BranchId, cancellationToken);
        return await SaveAsync(
            () => MapVehicle(entity, branchName!),
            active ? "Activated" : "Deactivated",
            "Vehicle",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<PagedResponse<TrailerItem>>> ListTrailersAsync(
        MasterDataQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<PagedResponse<TrailerItem>>(LoadIxPermissions.FleetView);
        var invalid = ValidateQuery<TrailerItem>(query);
        if (denied is not null) return denied;
        if (invalid is not null) return invalid;
        var source = dbContext.Trailers.AsNoTracking();
        source = query.Status switch
        {
            "active" => source.Where(item => item.IsActive),
            "inactive" => source.Where(item => !item.IsActive),
            _ => source,
        };
        if (query.BranchId.HasValue)
            source = source.Where(item => item.BranchId == query.BranchId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item =>
                item.Registration.Contains(search)
                || item.FleetNumber.Contains(search)
                || item.Make.Contains(search)
                || item.Model.Contains(search));
        }

        source = OrderTrailers(source, query);
        var total = await source.CountAsync(cancellationToken);
        var entities = await source.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var branches = await BranchNamesAsync(
            entities.Select(item => item.BranchId), cancellationToken);
        return MasterDataResult<PagedResponse<TrailerItem>>.Success(
            Page(
                entities.Select(item => MapTrailer(item, branches[item.BranchId])).ToArray(),
                query,
                total));
    }

    public async Task<MasterDataResult<TrailerItem>> GetTrailerAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<TrailerItem>(LoadIxPermissions.FleetView);
        if (denied is not null) return denied;
        var entity = await dbContext.Trailers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<TrailerItem>("Trailer");
        var branchName = await BranchNameAsync(entity.BranchId, cancellationToken);
        return MasterDataResult<TrailerItem>.Success(MapTrailer(entity, branchName!));
    }

    public async Task<MasterDataResult<TrailerItem>> CreateTrailerAsync(
        TrailerWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<TrailerItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var values = await ValidateTrailerWriteAsync(model, cancellationToken);
        if (!values.IsSuccess) return values.Result!;
        var result = Trailer.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.BranchId,
            model.Registration,
            model.FleetNumber,
            model.Make,
            model.Model,
            model.Year,
            values.Tare!,
            values.Capacity!,
            model.LicenceExpiry);
        if (result.IsFailure) return Validation<TrailerItem>(result.Error!);
        result.Value.SetAvailability(model.IsAvailable);
        dbContext.Trailers.Add(result.Value);
        return await SaveAsync(
            () => MapTrailer(result.Value, values.BranchName!),
            "Created",
            "Trailer",
            result.Value.Id,
            cancellationToken);
    }

    public async Task<MasterDataResult<TrailerItem>> UpdateTrailerAsync(
        Guid id, TrailerWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<TrailerItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Trailers.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<TrailerItem>("Trailer");
        if (!TryApplyVersion(dbContext, entity, model.Version))
            return InvalidVersion<TrailerItem>();
        var values = await ValidateTrailerWriteAsync(model, cancellationToken);
        if (!values.IsSuccess) return values.Result!;
        var result = entity.Update(
            model.BranchId,
            model.Registration,
            model.FleetNumber,
            model.Make,
            model.Model,
            model.Year,
            values.Tare!,
            values.Capacity!,
            model.LicenceExpiry,
            model.IsAvailable);
        if (result.IsFailure) return Validation<TrailerItem>(result.Error!);
        return await SaveAsync(
            () => MapTrailer(entity, values.BranchName!),
            "Updated",
            "Trailer",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<TrailerItem>> SetTrailerActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken)
    {
        var denied = Authorize<TrailerItem>(LoadIxPermissions.FleetManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Trailers.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<TrailerItem>("Trailer");
        if (!TryApplyVersion(dbContext, entity, version))
            return InvalidVersion<TrailerItem>();
        if (active) entity.Activate(); else entity.Deactivate();
        var branchName = await BranchNameAsync(entity.BranchId, cancellationToken);
        return await SaveAsync(
            () => MapTrailer(entity, branchName!),
            active ? "Activated" : "Deactivated",
            "Trailer",
            id,
            cancellationToken);
    }

    private async Task<string?> BranchNameAsync(
        Guid branchId, CancellationToken cancellationToken) =>
        await dbContext.Branches
            .Where(item => item.Id == branchId && item.IsActive)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Dictionary<Guid, string>> BranchNamesAsync(
        IEnumerable<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        var ids = branchIds.Distinct().ToArray();
        return await dbContext.Branches.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
    }

    private static MasterDataResult<T> InvalidBranch<T>() =>
        MasterDataResult<T>.Fail(
            MasterDataFailure.Validation,
            "fleet.branch_invalid",
            "The selected active branch was not found.");

    private static MasterDataResult<(Weight Tare, Weight Capacity)> Weights(
        decimal tareValue, decimal capacityValue, string unitValue)
    {
        if (!Enum.TryParse<WeightUnit>(unitValue, true, out var unit))
            return MasterDataResult<(Weight, Weight)>.Fail(
                MasterDataFailure.Validation,
                "weight.invalid_unit",
                "Weight unit must be Kilograms or Tonnes.");
        var tare = Weight.Create(tareValue, unit);
        var capacity = Weight.Create(capacityValue, unit);
        if (tare.IsFailure) return Validation<(Weight, Weight)>(tare.Error!);
        if (capacity.IsFailure) return Validation<(Weight, Weight)>(capacity.Error!);
        return MasterDataResult<(Weight, Weight)>.Success((tare.Value, capacity.Value));
    }

    private async Task<(
        bool IsSuccess,
        Weight? Tare,
        Weight? Capacity,
        string? BranchName,
        MasterDataResult<VehicleItem>? Result)> ValidateVehicleWriteAsync(
            VehicleWriteModel model,
            CancellationToken cancellationToken)
    {
        var branchName = await BranchNameAsync(model.BranchId, cancellationToken);
        if (branchName is null)
            return (false, null, null, null, InvalidBranch<VehicleItem>());
        var weights = Weights(model.TareWeight, model.CarryingCapacity, model.WeightUnit);
        if (!weights.IsSuccess)
            return (
                false,
                null,
                null,
                branchName,
                MasterDataResult<VehicleItem>.Fail(
                    weights.Failure, weights.Code, weights.Message));
        return (true, weights.Value.Tare, weights.Value.Capacity, branchName, null);
    }

    private async Task<(
        bool IsSuccess,
        Weight? Tare,
        Weight? Capacity,
        string? BranchName,
        MasterDataResult<TrailerItem>? Result)> ValidateTrailerWriteAsync(
            TrailerWriteModel model,
            CancellationToken cancellationToken)
    {
        var branchName = await BranchNameAsync(model.BranchId, cancellationToken);
        if (branchName is null)
            return (false, null, null, null, InvalidBranch<TrailerItem>());
        var weights = Weights(model.TareWeight, model.CarryingCapacity, model.WeightUnit);
        if (!weights.IsSuccess)
            return (
                false,
                null,
                null,
                branchName,
                MasterDataResult<TrailerItem>.Fail(
                    weights.Failure, weights.Code, weights.Message));
        return (true, weights.Value.Tare, weights.Value.Capacity, branchName, null);
    }

    private static IQueryable<Vehicle> OrderVehicles(
        IQueryable<Vehicle> source, MasterDataQuery query)
    {
        var descending = query.Direction == "desc";
        return (query.Sort?.ToLowerInvariant(), descending) switch
        {
            ("registration", true) => source.OrderByDescending(item => item.Registration)
                .ThenByDescending(item => item.Id),
            ("registration", false) => source.OrderBy(item => item.Registration)
                .ThenBy(item => item.Id),
            ("expiry", true) => source.OrderByDescending(item => item.LicenceExpiry)
                .ThenByDescending(item => item.Id),
            ("expiry", false) => source.OrderBy(item => item.LicenceExpiry)
                .ThenBy(item => item.Id),
            (_, true) => source.OrderByDescending(item => item.FleetNumber)
                .ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.FleetNumber).ThenBy(item => item.Id),
        };
    }

    private static IQueryable<Trailer> OrderTrailers(
        IQueryable<Trailer> source, MasterDataQuery query)
    {
        var descending = query.Direction == "desc";
        return (query.Sort?.ToLowerInvariant(), descending) switch
        {
            ("registration", true) => source.OrderByDescending(item => item.Registration)
                .ThenByDescending(item => item.Id),
            ("registration", false) => source.OrderBy(item => item.Registration)
                .ThenBy(item => item.Id),
            ("expiry", true) => source.OrderByDescending(item => item.LicenceExpiry)
                .ThenByDescending(item => item.Id),
            ("expiry", false) => source.OrderBy(item => item.LicenceExpiry)
                .ThenBy(item => item.Id),
            (_, true) => source.OrderByDescending(item => item.FleetNumber)
                .ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.FleetNumber).ThenBy(item => item.Id),
        };
    }

    private DriverItem MapDriver(Driver entity, string branchName) =>
        new(
            entity.Id,
            entity.LicenceNumber,
            entity.BranchId,
            branchName,
            entity.FirstName,
            entity.LastName,
            entity.Type.ToString(),
            entity.IdentificationReference,
            entity.MobileNumber,
            entity.LicenceNumber,
            entity.LicenceExpiry,
            ComplianceStatus(entity.LicenceExpiry),
            entity.EmergencyContactName,
            entity.EmergencyContactNumber,
            entity.IsActive,
            Version(entity.RowVersion),
            entity.ModifiedAtUtc);

    private VehicleItem MapVehicle(Vehicle entity, string branchName) =>
        new(
            entity.Id,
            entity.FleetNumber,
            entity.BranchId,
            branchName,
            entity.Registration,
            entity.Make,
            entity.Model,
            entity.Year,
            entity.TareWeight.Value,
            entity.CarryingCapacity.Value,
            entity.TareWeight.Unit.ToString(),
            entity.RoadworthyExpiry,
            entity.LicenceExpiry,
            ComplianceStatus(entity.RoadworthyExpiry, entity.LicenceExpiry),
            entity.IsAvailable,
            entity.IsActive,
            Version(entity.RowVersion),
            entity.ModifiedAtUtc);

    private TrailerItem MapTrailer(Trailer entity, string branchName) =>
        new(
            entity.Id,
            entity.FleetNumber,
            entity.BranchId,
            branchName,
            entity.Registration,
            entity.Make,
            entity.Model,
            entity.Year,
            entity.TareWeight.Value,
            entity.CarryingCapacity.Value,
            entity.TareWeight.Unit.ToString(),
            entity.LicenceExpiry,
            ComplianceStatus(entity.LicenceExpiry),
            entity.IsAvailable,
            entity.IsActive,
            Version(entity.RowVersion),
            entity.ModifiedAtUtc);
}
