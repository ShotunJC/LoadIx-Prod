using LoadIx.Application.MasterData;
using LoadIx.Application.Security;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.MasterData;

public sealed partial class MasterDataService
{
    public async Task<MasterDataResult<PagedResponse<BranchItem>>> ListBranchesAsync(
        MasterDataQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<PagedResponse<BranchItem>>(LoadIxPermissions.TenantView);
        var invalid = ValidateQuery<BranchItem>(query);
        if (denied is not null) return denied;
        if (invalid is not null) return invalid;

        var source = dbContext.Branches.AsNoTracking();
        source = query.Status switch
        {
            "active" => source.Where(item => item.IsActive),
            "inactive" => source.Where(item => !item.IsActive),
            _ => source,
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item =>
                item.Name.Contains(search) || item.Code.Contains(search));
        }

        var descending = query.Direction == "desc";
        source = (query.Sort?.ToLowerInvariant(), descending) switch
        {
            ("code", true) => source.OrderByDescending(item => item.Code)
                .ThenByDescending(item => item.Id),
            ("code", false) => source.OrderBy(item => item.Code).ThenBy(item => item.Id),
            ("status", true) => source.OrderBy(item => item.IsActive).ThenBy(item => item.Id),
            ("status", false) => source.OrderByDescending(item => item.IsActive)
                .ThenBy(item => item.Id),
            (_, true) => source.OrderByDescending(item => item.Name)
                .ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.Name).ThenBy(item => item.Id),
        };

        var total = await source.CountAsync(cancellationToken);
        var entities = await source.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return MasterDataResult<PagedResponse<BranchItem>>.Success(
            Page(entities.Select(MapBranch).ToArray(), query, total));
    }

    public async Task<MasterDataResult<BranchItem>> GetBranchAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<BranchItem>(LoadIxPermissions.TenantView);
        if (denied is not null) return denied;
        var entity = await dbContext.Branches.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null
            ? NotFound<BranchItem>("Branch")
            : MasterDataResult<BranchItem>.Success(MapBranch(entity));
    }

    public async Task<MasterDataResult<BranchItem>> CreateBranchAsync(
        BranchWriteModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<BranchItem>(LoadIxPermissions.TenantManage);
        if (denied is not null) return denied;
        var address = CreateAddress(model.Address);
        if (!address.IsSuccess)
        {
            return MasterDataResult<BranchItem>.Fail(
                address.Failure, address.Code, address.Message);
        }

        var result = Branch.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.Name,
            model.Code,
            address.Value!,
            model.ContactEmail,
            model.ContactPhoneNumber);
        if (result.IsFailure) return Validation<BranchItem>(result.Error!);
        dbContext.Branches.Add(result.Value);
        return await SaveAsync(
            () => MapBranch(result.Value),
            "Created",
            "Branch",
            result.Value.Id,
            cancellationToken);
    }

    public async Task<MasterDataResult<BranchItem>> UpdateBranchAsync(
        Guid id,
        BranchWriteModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<BranchItem>(LoadIxPermissions.TenantManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Branches.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<BranchItem>("Branch");
        if (!TryApplyVersion(dbContext, entity, model.Version))
            return InvalidVersion<BranchItem>();
        var address = CreateAddress(model.Address);
        if (!address.IsSuccess)
            return MasterDataResult<BranchItem>.Fail(
                address.Failure, address.Code, address.Message);
        var result = entity.Update(
            model.Name,
            model.Code,
            address.Value!,
            model.ContactEmail,
            model.ContactPhoneNumber);
        if (result.IsFailure) return Validation<BranchItem>(result.Error!);
        return await SaveAsync(
            () => MapBranch(entity), "Updated", "Branch", id, cancellationToken);
    }

    public Task<MasterDataResult<BranchItem>> SetBranchActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken) =>
        SetBranchActiveCoreAsync(id, active, version, cancellationToken);

    private async Task<MasterDataResult<BranchItem>> SetBranchActiveCoreAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken)
    {
        var denied = Authorize<BranchItem>(LoadIxPermissions.TenantManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Branches.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<BranchItem>("Branch");
        if (!TryApplyVersion(dbContext, entity, version))
            return InvalidVersion<BranchItem>();
        if (active) entity.Activate(); else entity.Deactivate();
        return await SaveAsync(
            () => MapBranch(entity),
            active ? "Activated" : "Deactivated",
            "Branch",
            id,
            cancellationToken);
    }

    public async Task<MasterDataResult<PagedResponse<CommodityItem>>> ListCommoditiesAsync(
        MasterDataQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<PagedResponse<CommodityItem>>(LoadIxPermissions.ContractView);
        var invalid = ValidateQuery<CommodityItem>(query);
        if (denied is not null) return denied;
        if (invalid is not null) return invalid;
        var source = dbContext.Commodities.AsNoTracking();
        source = query.Status switch
        {
            "active" => source.Where(item => item.IsActive),
            "inactive" => source.Where(item => !item.IsActive),
            _ => source,
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(item =>
                item.Name.Contains(search) || item.Code.Contains(search));
        }

        var descending = query.Direction == "desc";
        source = (query.Sort?.ToLowerInvariant(), descending) switch
        {
            ("code", true) => source.OrderByDescending(item => item.Code)
                .ThenByDescending(item => item.Id),
            ("code", false) => source.OrderBy(item => item.Code).ThenBy(item => item.Id),
            (_, true) => source.OrderByDescending(item => item.Name)
                .ThenByDescending(item => item.Id),
            _ => source.OrderBy(item => item.Name).ThenBy(item => item.Id),
        };
        var total = await source.CountAsync(cancellationToken);
        var entities = await source.Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return MasterDataResult<PagedResponse<CommodityItem>>.Success(
            Page(entities.Select(MapCommodity).ToArray(), query, total));
    }

    public async Task<MasterDataResult<CommodityItem>> GetCommodityAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var denied = Authorize<CommodityItem>(LoadIxPermissions.ContractView);
        if (denied is not null) return denied;
        var entity = await dbContext.Commodities.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null
            ? NotFound<CommodityItem>("Commodity")
            : MasterDataResult<CommodityItem>.Success(MapCommodity(entity));
    }

    public async Task<MasterDataResult<CommodityItem>> CreateCommodityAsync(
        CommodityWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<CommodityItem>(LoadIxPermissions.ContractManage);
        if (denied is not null) return denied;
        if (!Enum.TryParse<WeightUnit>(model.DefaultWeightUnit, true, out var unit))
            return MasterDataResult<CommodityItem>.Fail(
                MasterDataFailure.Validation,
                "commodity.invalid_unit",
                "Weight unit must be Kilogram or Tonne.");
        var result = Commodity.Create(
            Guid.NewGuid(),
            requestContext.TenantId!.Value,
            model.Name,
            model.Code,
            unit);
        if (result.IsFailure) return Validation<CommodityItem>(result.Error!);
        dbContext.Commodities.Add(result.Value);
        return await SaveAsync(
            () => MapCommodity(result.Value),
            "Created",
            "Commodity",
            result.Value.Id,
            cancellationToken);
    }

    public async Task<MasterDataResult<CommodityItem>> UpdateCommodityAsync(
        Guid id, CommodityWriteModel model, CancellationToken cancellationToken)
    {
        var denied = Authorize<CommodityItem>(LoadIxPermissions.ContractManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Commodities.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<CommodityItem>("Commodity");
        if (!TryApplyVersion(dbContext, entity, model.Version))
            return InvalidVersion<CommodityItem>();
        if (!Enum.TryParse<WeightUnit>(model.DefaultWeightUnit, true, out var unit))
            return MasterDataResult<CommodityItem>.Fail(
                MasterDataFailure.Validation,
                "commodity.invalid_unit",
                "Weight unit must be Kilogram or Tonne.");
        var result = entity.Update(model.Name, model.Code, unit);
        if (result.IsFailure) return Validation<CommodityItem>(result.Error!);
        return await SaveAsync(
            () => MapCommodity(entity), "Updated", "Commodity", id, cancellationToken);
    }

    public async Task<MasterDataResult<CommodityItem>> SetCommodityActiveAsync(
        Guid id, bool active, string version, CancellationToken cancellationToken)
    {
        var denied = Authorize<CommodityItem>(LoadIxPermissions.ContractManage);
        if (denied is not null) return denied;
        var entity = await dbContext.Commodities.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (entity is null) return NotFound<CommodityItem>("Commodity");
        if (!TryApplyVersion(dbContext, entity, version))
            return InvalidVersion<CommodityItem>();
        if (active) entity.Activate(); else entity.Deactivate();
        return await SaveAsync(
            () => MapCommodity(entity),
            active ? "Activated" : "Deactivated",
            "Commodity",
            id,
            cancellationToken);
    }

    private static PagedResponse<T> Page<T>(
        IReadOnlyList<T> items, MasterDataQuery query, int total) =>
        new(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));

    private static BranchItem MapBranch(Branch entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.Name,
            MapAddress(entity.Address),
            entity.ContactEmail,
            entity.ContactPhoneNumber,
            entity.IsActive,
            Version(entity.RowVersion),
            entity.ModifiedAtUtc);

    private static CommodityItem MapCommodity(Commodity entity) =>
        new(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.DefaultWeightUnit.ToString(),
            entity.IsActive,
            Version(entity.RowVersion),
            entity.ModifiedAtUtc);
}
