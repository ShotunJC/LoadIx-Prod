using LoadIx.Application.Abstractions;
using LoadIx.Application.MasterData;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.MasterData;

public sealed partial class MasterDataService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    SecurityAuditWriter auditWriter,
    TimeProvider timeProvider) : IMasterDataService
{
    private const int MaximumPageSize = 100;

    private MasterDataResult<T>? Authorize<T>(string permission)
    {
        if (!requestContext.TenantId.HasValue
            || !requestContext.HasPermission(permission))
        {
            return MasterDataResult<T>.Fail(
                MasterDataFailure.Forbidden,
                "authorization.denied",
                "You do not have permission to perform this operation.");
        }

        return null;
    }

    private static MasterDataResult<PagedResponse<T>>? ValidateQuery<T>(
        MasterDataQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > MaximumPageSize)
        {
            return MasterDataResult<PagedResponse<T>>.Fail(
                MasterDataFailure.Validation,
                "pagination.invalid",
                $"Page must be positive and pageSize must be between 1 and {MaximumPageSize}.");
        }

        if (query.Direction is not ("asc" or "desc")
            || query.Status is not ("active" or "inactive" or "all"))
        {
            return MasterDataResult<PagedResponse<T>>.Fail(
                MasterDataFailure.Validation,
                "filters.invalid",
                "Direction or status filter is invalid.");
        }

        return null;
    }

    private static MasterDataResult<Address> CreateAddress(AddressModel model)
    {
        var result = Address.Create(
            model.Line1,
            model.Line2,
            model.City,
            model.ProvinceOrRegion,
            model.PostalCode,
            model.CountryCode);
        return result.IsSuccess
            ? MasterDataResult<Address>.Success(result.Value)
            : Validation<Address>(result.Error!);
    }

    private static AddressModel MapAddress(Address address) =>
        new(
            address.Line1,
            address.Line2,
            address.City,
            address.ProvinceOrRegion,
            address.PostalCode,
            address.CountryCode);

    private static string Version(byte[] rowVersion) =>
        Convert.ToBase64String(rowVersion);

    private static bool TryApplyVersion<TEntity>(
        LoadIxDbContext context,
        TEntity entity,
        string? version)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        try
        {
            context.Entry(entity).Property("RowVersion").OriginalValue =
                Convert.FromBase64String(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MasterDataResult<T> Validation<T>(DomainError error) =>
        MasterDataResult<T>.Fail(
            MasterDataFailure.Validation,
            error.Code,
            error.Description);

    private static MasterDataResult<T> NotFound<T>(string resource) =>
        MasterDataResult<T>.Fail(
            MasterDataFailure.NotFound,
            $"{resource.ToLowerInvariant()}.not_found",
            $"The requested {resource.ToLowerInvariant()} was not found.");

    private static MasterDataResult<T> InvalidVersion<T>() =>
        MasterDataResult<T>.Fail(
            MasterDataFailure.Validation,
            "concurrency.version_required",
            "A valid version is required for an update.");

    private static MasterDataResult<T> Conflict<T>(string message) =>
        MasterDataResult<T>.Fail(
            MasterDataFailure.Conflict,
            "concurrency.conflict",
            message);

    private async Task<MasterDataResult<T>> SaveAsync<T>(
        Func<T> valueFactory,
        string action,
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditWriter.WriteAsync(
                $"MasterData.{action}",
                "Succeeded",
                targetType,
                targetId.ToString(),
                requestContext.TenantId,
                requestContext.UserId,
                requestContext.IsSupportContext,
                null,
                cancellationToken);
            return MasterDataResult<T>.Success(valueFactory());
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<T>(
                "The record changed after it was loaded. Refresh and try again.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException?.Message.Contains(
                "duplicate",
                StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains(
                "UNIQUE",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return MasterDataResult<T>.Fail(
                MasterDataFailure.Conflict,
                "master_data.duplicate",
                "A record with the same reference already exists.");
        }
    }

    private string ComplianceStatus(params DateOnly[] expiryDates)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var earliest = expiryDates.Min();
        if (earliest < today)
        {
            return "Expired";
        }

        return earliest <= today.AddDays(30) ? "Expiring" : "Valid";
    }
}
