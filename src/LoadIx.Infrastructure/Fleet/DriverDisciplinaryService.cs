using LoadIx.Application.Abstractions;
using LoadIx.Application.Fleet;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Fleet;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Fleet;

public sealed class DriverDisciplinaryService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    TimeProvider timeProvider,
    SecurityAuditWriter auditWriter)
    : IDriverDisciplinaryService
{
    public async Task<Result<DriverWarningDetail>> IssueWarningAsync(
        Guid driverId,
        DriverWarningCreateModel model,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetWarningManage);
        if (authResult.IsFailure)
        {
            return Result<DriverWarningDetail>.Failure(authResult.Error!);
        }

        var driver = await dbContext.Drivers
            .SingleOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return Result<DriverWarningDetail>.Failure(new DomainError("driver.not_found", "Driver not found."));
        }

        var warningId = Guid.NewGuid();
        var warning = DriverWarning.Create(
            warningId,
            requestContext.TenantId!.Value,
            driverId,
            model.WarningNumber,
            model.WarningType,
            model.Category,
            model.IncidentDateUtc,
            model.IssuedDateUtc,
            model.ExpiryDateUtc,
            model.Description,
            requestContext.UserId!.Value,
            model.TripId,
            model.DocumentId);

        if (warning.IsFailure)
        {
            return Result<DriverWarningDetail>.Failure(warning.Error!);
        }

        dbContext.DriverWarnings.Add(warning.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Fleet.DriverWarning.Issued",
            "Succeeded",
            "DriverWarning",
            warningId.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                driverId,
                warningNumber = warning.Value.WarningNumber,
                warningType = warning.Value.WarningType.ToString(),
                category = warning.Value.Category.ToString(),
            }));

        return Result<DriverWarningDetail>.Success(MapWarning(warning.Value));
    }

    public async Task<Result<DriverWarningDetail>> RevokeWarningAsync(
        Guid warningId,
        RevokeWarningModel model,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetWarningManage);
        if (authResult.IsFailure)
        {
            return Result<DriverWarningDetail>.Failure(authResult.Error!);
        }

        var warning = await dbContext.DriverWarnings
            .SingleOrDefaultAsync(w => w.Id == warningId, cancellationToken);

        if (warning is null)
        {
            return Result<DriverWarningDetail>.Failure(new DomainError("driver_warning.not_found", "Warning not found."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var revokeResult = warning.Revoke(model.Reason, requestContext.UserId!.Value, nowUtc);
        if (revokeResult.IsFailure)
        {
            return Result<DriverWarningDetail>.Failure(revokeResult.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Fleet.DriverWarning.Revoked",
            "Succeeded",
            "DriverWarning",
            warningId.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                warningId,
                reason = model.Reason,
            }));

        return Result<DriverWarningDetail>.Success(MapWarning(warning));
    }

    public async Task<Result<IReadOnlyList<DriverWarningDetail>>> GetDriverWarningsAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetWarningView);
        if (authResult.IsFailure)
        {
            return Result<IReadOnlyList<DriverWarningDetail>>.Failure(authResult.Error!);
        }

        var driverExists = await dbContext.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);
        if (!driverExists)
        {
            return Result<IReadOnlyList<DriverWarningDetail>>.Failure(new DomainError("driver.not_found", "Driver not found."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var warnings = await dbContext.DriverWarnings
            .Where(w => w.DriverId == driverId)
            .OrderByDescending(w => w.IncidentDateUtc)
            .ToListAsync(cancellationToken);

        var hasUpdates = false;
        foreach (var warning in warnings)
        {
            var oldStatus = warning.Status;
            warning.CheckAndApplyExpiry(nowUtc);
            if (warning.Status != oldStatus)
            {
                hasUpdates = true;
            }
        }

        if (hasUpdates)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result<IReadOnlyList<DriverWarningDetail>>.Success(warnings.Select(MapWarning).ToList());
    }

    public async Task<Result<DriverDeductionDetail>> CreateDeductionAsync(
        Guid driverId,
        DriverDeductionCreateModel model,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetDeductionManage);
        if (authResult.IsFailure)
        {
            return Result<DriverDeductionDetail>.Failure(authResult.Error!);
        }

        var driver = await dbContext.Drivers
            .SingleOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return Result<DriverDeductionDetail>.Failure(new DomainError("driver.not_found", "Driver not found."));
        }

        if (model.WarningId.HasValue)
        {
            var warningExists = await dbContext.DriverWarnings.AnyAsync(w => w.Id == model.WarningId.Value && w.DriverId == driverId, cancellationToken);
            if (!warningExists)
            {
                return Result<DriverDeductionDetail>.Failure(new DomainError("driver_deduction.warning_not_found", "Linked warning not found for this driver."));
            }
        }

        var deductionId = Guid.NewGuid();
        var nowUtc = timeProvider.GetUtcNow();

        var deduction = DriverDeduction.Create(
            deductionId,
            requestContext.TenantId!.Value,
            driverId,
            model.DeductionNumber,
            model.Category,
            model.CurrencyCode,
            model.TotalAmount,
            model.InstallmentAmount,
            model.EffectiveDate,
            model.Description,
            nowUtc,
            model.WarningId,
            model.TripId,
            model.ConsentDocumentId);

        if (deduction.IsFailure)
        {
            return Result<DriverDeductionDetail>.Failure(deduction.Error!);
        }

        dbContext.DriverDeductions.Add(deduction.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Fleet.DriverDeduction.Created",
            "Succeeded",
            "DriverDeduction",
            deductionId.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                driverId,
                deductionNumber = deduction.Value.DeductionNumber,
                totalAmount = deduction.Value.TotalAmount,
                installmentAmount = deduction.Value.InstallmentAmount,
            }));

        return Result<DriverDeductionDetail>.Success(MapDeduction(deduction.Value));
    }

    public async Task<Result<DriverDeductionDetail>> ApproveDeductionAsync(
        Guid deductionId,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetDeductionManage);
        if (authResult.IsFailure)
        {
            return Result<DriverDeductionDetail>.Failure(authResult.Error!);
        }

        var deduction = await dbContext.DriverDeductions
            .SingleOrDefaultAsync(d => d.Id == deductionId, cancellationToken);

        if (deduction is null)
        {
            return Result<DriverDeductionDetail>.Failure(new DomainError("driver_deduction.not_found", "Deduction not found."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var approveResult = deduction.Approve(requestContext.UserId!.Value, nowUtc);
        if (approveResult.IsFailure)
        {
            return Result<DriverDeductionDetail>.Failure(approveResult.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Fleet.DriverDeduction.Approved",
            "Succeeded",
            "DriverDeduction",
            deductionId.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                deductionId,
                totalAmount = deduction.TotalAmount,
            }));

        return Result<DriverDeductionDetail>.Success(MapDeduction(deduction));
    }

    public async Task<Result<DriverDeductionDetail>> CancelDeductionAsync(
        Guid deductionId,
        CancelDeductionModel model,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetDeductionManage);
        if (authResult.IsFailure)
        {
            return Result<DriverDeductionDetail>.Failure(authResult.Error!);
        }

        var deduction = await dbContext.DriverDeductions
            .SingleOrDefaultAsync(d => d.Id == deductionId, cancellationToken);

        if (deduction is null)
        {
            return Result<DriverDeductionDetail>.Failure(new DomainError("driver_deduction.not_found", "Deduction not found."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var cancelResult = deduction.Cancel(model.Reason, requestContext.UserId!.Value, nowUtc);
        if (cancelResult.IsFailure)
        {
            return Result<DriverDeductionDetail>.Failure(cancelResult.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Fleet.DriverDeduction.Cancelled",
            "Succeeded",
            "DriverDeduction",
            deductionId.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                deductionId,
                reason = model.Reason,
            }));

        return Result<DriverDeductionDetail>.Success(MapDeduction(deduction));
    }

    public async Task<Result<IReadOnlyList<DriverDeductionDetail>>> GetDriverDeductionsAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetDeductionView);
        if (authResult.IsFailure)
        {
            return Result<IReadOnlyList<DriverDeductionDetail>>.Failure(authResult.Error!);
        }

        var driverExists = await dbContext.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);
        if (!driverExists)
        {
            return Result<IReadOnlyList<DriverDeductionDetail>>.Failure(new DomainError("driver.not_found", "Driver not found."));
        }

        var deductions = await dbContext.DriverDeductions
            .Where(d => d.DriverId == driverId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DriverDeductionDetail>>.Success(deductions.Select(MapDeduction).ToList());
    }

    public async Task<Result<DriverDisciplinarySummary>> GetDriverDisciplinarySummaryAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FleetWarningView);
        if (authResult.IsFailure)
        {
            return Result<DriverDisciplinarySummary>.Failure(authResult.Error!);
        }

        var driver = await dbContext.Drivers
            .SingleOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        if (driver is null)
        {
            return Result<DriverDisciplinarySummary>.Failure(new DomainError("driver.not_found", "Driver not found."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var warnings = await dbContext.DriverWarnings
            .Where(w => w.DriverId == driverId)
            .OrderByDescending(w => w.IncidentDateUtc)
            .ToListAsync(cancellationToken);

        foreach (var warning in warnings)
        {
            warning.CheckAndApplyExpiry(nowUtc);
        }

        var deductions = await dbContext.DriverDeductions
            .Where(d => d.DriverId == driverId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var activeWarnings = warnings.Where(w => w.Status == DriverWarningStatus.Active).Select(MapWarning).ToList();
        var activeDeductions = deductions.Where(d => d.Status == DriverDeductionStatus.Active).Select(MapDeduction).ToList();

        var summary = new DriverDisciplinarySummary(
            driver.Id,
            driver.DisplayName,
            driver.EmployeeNumber,
            activeWarnings.Count,
            warnings.Count,
            deductions.Where(d => d.Status == DriverDeductionStatus.Active).Sum(d => d.RemainingBalance),
            activeWarnings,
            activeDeductions);

        return Result<DriverDisciplinarySummary>.Success(summary);
    }

    private Result Authorize(string permission)
    {
        if (!requestContext.TenantId.HasValue || requestContext.TenantId.Value.IsEmpty)
        {
            return Result.Failure(new DomainError("authorization.tenant_required", "Tenant context is required."));
        }

        if (!requestContext.HasPermission(permission))
        {
            return Result.Failure(new DomainError("authorization.denied", $"Permission '{permission}' is required."));
        }

        return Result.Success();
    }

    private static DriverWarningDetail MapWarning(DriverWarning warning) =>
        new(
            warning.Id,
            warning.TenantId.Value,
            warning.DriverId,
            warning.WarningNumber,
            warning.WarningType,
            warning.Category,
            warning.Status,
            warning.IncidentDateUtc,
            warning.IssuedDateUtc,
            warning.ExpiryDateUtc,
            warning.Description,
            warning.IssuedByUserId,
            warning.TripId,
            warning.DocumentId,
            warning.RevokedByUserId,
            warning.RevokedAtUtc,
            warning.RevocationReason);

    private static DriverDeductionDetail MapDeduction(DriverDeduction deduction) =>
        new(
            deduction.Id,
            deduction.TenantId.Value,
            deduction.DriverId,
            deduction.DeductionNumber,
            deduction.Category,
            deduction.CurrencyCode,
            deduction.TotalAmount,
            deduction.InstallmentAmount,
            deduction.RemainingBalance,
            deduction.EffectiveDate,
            deduction.Description,
            deduction.Status,
            deduction.CreatedAtUtc,
            deduction.WarningId,
            deduction.TripId,
            deduction.ConsentDocumentId,
            deduction.ApprovedByUserId,
            deduction.ApprovedAtUtc,
            deduction.CancelledByUserId,
            deduction.CancelledAtUtc,
            deduction.CancellationReason);
}
