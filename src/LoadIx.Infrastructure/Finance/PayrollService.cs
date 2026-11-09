using LoadIx.Application.Abstractions;
using LoadIx.Application.Finance;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Finance;
using LoadIx.Domain.Fleet;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Finance;

public sealed class PayrollService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    TimeProvider timeProvider,
    SecurityAuditWriter auditWriter)
    : IPayrollService
{
    public async Task<Result<PayrollBatchPreview>> PreviewBatchAsync(
        PayrollBatchPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollView);
        if (authResult.IsFailure)
        {
            return Result<PayrollBatchPreview>.Failure(authResult.Error!);
        }

        var activeDeductions = await dbContext.DriverDeductions
            .Where(d => d.Status == DriverDeductionStatus.Active
                && d.RemainingBalance > 0m
                && d.EffectiveDate <= request.PeriodEndDate)
            .ToListAsync(cancellationToken);

        var driverIds = activeDeductions.Select(d => d.DriverId).Distinct().ToList();
        var drivers = await dbContext.Drivers
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var previewLines = new List<PayrollBatchPreviewLine>();

        foreach (var deduction in activeDeductions)
        {
            if (!drivers.TryGetValue(deduction.DriverId, out var driver))
            {
                continue;
            }

            var installment = Math.Min(deduction.InstallmentAmount, deduction.RemainingBalance);
            var balanceAfter = Math.Max(0m, deduction.RemainingBalance - installment);
            var transCode = GetTransactionCode(deduction.Category);

            previewLines.Add(new PayrollBatchPreviewLine(
                driver.Id,
                driver.EmployeeNumber,
                driver.DisplayName,
                deduction.Id,
                deduction.DeductionNumber,
                transCode,
                deduction.Description,
                installment,
                deduction.RemainingBalance,
                balanceAfter));
        }

        var distinctDrivers = previewLines.Select(l => l.DriverId).Distinct().Count();
        var totalAmount = previewLines.Sum(l => l.InstallmentAmount);

        return Result<PayrollBatchPreview>.Success(new PayrollBatchPreview(
            request.PeriodStartDate,
            request.PeriodEndDate,
            request.Frequency,
            "ZAR",
            distinctDrivers,
            previewLines.Count,
            totalAmount,
            previewLines));
    }

    public async Task<Result<PayrollBatchDetail>> CreateBatchAsync(
        PayrollBatchCreateModel model,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollManage);
        if (authResult.IsFailure)
        {
            return Result<PayrollBatchDetail>.Failure(authResult.Error!);
        }

        var exists = await dbContext.PayrollBatches
            .AnyAsync(b => b.BatchNumber == model.BatchNumber.Trim(), cancellationToken);

        if (exists)
        {
            return Result<PayrollBatchDetail>.Failure(new DomainError(
                "payroll_batch.duplicate_number",
                "A payroll batch with this batch number already exists."));
        }

        var batchId = Guid.NewGuid();
        var nowUtc = timeProvider.GetUtcNow();

        var batchResult = PayrollBatch.Create(
            batchId,
            requestContext.TenantId!.Value,
            model.BatchNumber,
            model.PeriodStartDate,
            model.PeriodEndDate,
            model.Frequency,
            model.CurrencyCode,
            nowUtc);

        if (batchResult.IsFailure)
        {
            return Result<PayrollBatchDetail>.Failure(batchResult.Error!);
        }

        var batch = batchResult.Value;

        var activeDeductions = await dbContext.DriverDeductions
            .Where(d => d.Status == DriverDeductionStatus.Active
                && d.RemainingBalance > 0m
                && d.EffectiveDate <= model.PeriodEndDate)
            .ToListAsync(cancellationToken);

        var driverIds = activeDeductions.Select(d => d.DriverId).Distinct().ToList();
        var drivers = await dbContext.Drivers
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        foreach (var deduction in activeDeductions)
        {
            if (!drivers.TryGetValue(deduction.DriverId, out var driver))
            {
                continue;
            }

            var installment = Math.Min(deduction.InstallmentAmount, deduction.RemainingBalance);
            var balanceAfter = Math.Max(0m, deduction.RemainingBalance - installment);
            var transCode = GetTransactionCode(deduction.Category);

            var lineResult = PayrollBatchLine.Create(
                Guid.NewGuid(),
                requestContext.TenantId!.Value,
                batchId,
                driver.Id,
                driver.EmployeeNumber,
                driver.DisplayName,
                deduction.Id,
                transCode,
                deduction.Description,
                installment,
                balanceAfter);

            if (lineResult.IsSuccess)
            {
                batch.AddLine(lineResult.Value);
            }
        }

        dbContext.PayrollBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Finance.PayrollBatch.Created",
            "Succeeded",
            "PayrollBatch",
            batchId.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                batchNumber = batch.BatchNumber,
                lineCount = batch.Lines.Count,
                totalAmount = batch.TotalDeductionsAmount,
            }));

        return Result<PayrollBatchDetail>.Success(MapBatch(batch));
    }

    public async Task<Result<PayrollBatchDetail>> GetBatchAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollView);
        if (authResult.IsFailure)
        {
            return Result<PayrollBatchDetail>.Failure(authResult.Error!);
        }

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Lines)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (batch is null)
        {
            return Result<PayrollBatchDetail>.Failure(new DomainError("payroll_batch.not_found", "Payroll batch not found."));
        }

        return Result<PayrollBatchDetail>.Success(MapBatch(batch));
    }

    public async Task<Result<IReadOnlyList<PayrollBatchListItem>>> ListBatchesAsync(
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollView);
        if (authResult.IsFailure)
        {
            return Result<IReadOnlyList<PayrollBatchListItem>>.Failure(authResult.Error!);
        }

        var batches = await dbContext.PayrollBatches
            .Include(b => b.Lines)
            .OrderByDescending(b => b.PeriodStartDate)
            .ToListAsync(cancellationToken);

        var list = batches.Select(b => new PayrollBatchListItem(
            b.Id,
            b.BatchNumber,
            b.PeriodStartDate,
            b.PeriodEndDate,
            b.Frequency,
            b.CurrencyCode,
            b.Status,
            b.TotalDeductionsAmount,
            b.Lines.Count,
            b.CreatedAtUtc,
            b.ExportedAtUtc)).ToList();

        return Result<IReadOnlyList<PayrollBatchListItem>>.Success(list);
    }

    public async Task<Result<PayrollBatchDetail>> ApproveBatchAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollManage);
        if (authResult.IsFailure)
        {
            return Result<PayrollBatchDetail>.Failure(authResult.Error!);
        }

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Lines)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (batch is null)
        {
            return Result<PayrollBatchDetail>.Failure(new DomainError("payroll_batch.not_found", "Payroll batch not found."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var approveResult = batch.Approve(requestContext.UserId!.Value, nowUtc);
        if (approveResult.IsFailure)
        {
            return Result<PayrollBatchDetail>.Failure(approveResult.Error!);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Finance.PayrollBatch.Approved",
            "Succeeded",
            "PayrollBatch",
            id.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                batchId = id,
                totalAmount = batch.TotalDeductionsAmount,
            }));

        return Result<PayrollBatchDetail>.Success(MapBatch(batch));
    }

    public async Task<Result<PayrollBatchDetail>> MarkExportedAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollManage);
        if (authResult.IsFailure)
        {
            return Result<PayrollBatchDetail>.Failure(authResult.Error!);
        }

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Lines)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (batch is null)
        {
            return Result<PayrollBatchDetail>.Failure(new DomainError("payroll_batch.not_found", "Payroll batch not found."));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var exportResult = batch.MarkExported(requestContext.UserId!.Value, nowUtc);
        if (exportResult.IsFailure)
        {
            return Result<PayrollBatchDetail>.Failure(exportResult.Error!);
        }

        // Decrement balances on linked deductions
        var deductionIds = batch.Lines
            .Where(l => l.DeductionId.HasValue)
            .Select(l => l.DeductionId!.Value)
            .Distinct()
            .ToList();

        var deductions = await dbContext.DriverDeductions
            .Where(d => deductionIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        foreach (var line in batch.Lines)
        {
            if (line.DeductionId.HasValue && deductions.TryGetValue(line.DeductionId.Value, out var deduction))
            {
                deduction.ApplyInstallment(line.Amount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "Finance.PayrollBatch.Exported",
            "Succeeded",
            "PayrollBatch",
            id.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                batchId = id,
                lineCount = batch.Lines.Count,
                totalAmount = batch.TotalDeductionsAmount,
            }));

        return Result<PayrollBatchDetail>.Success(MapBatch(batch));
    }

    public async Task<Result<byte[]>> ExportPastelPayrollCsvAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollView);
        if (authResult.IsFailure)
        {
            return Result<byte[]>.Failure(authResult.Error!);
        }

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Lines)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (batch is null)
        {
            return Result<byte[]>.Failure(new DomainError("payroll_batch.not_found", "Payroll batch not found."));
        }

        var content = PastelPayrollBatchFormatter.Format(batch);

        await auditWriter.WriteAsync(
            "Finance.PayrollBatch.ExportFormat",
            "Succeeded",
            "PayrollBatch",
            id.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                format = "pastel_payroll",
                byteCount = content.Length,
            }));

        return Result<byte[]>.Success(content);
    }

    public async Task<Result<byte[]>> ExportGenericPayrollCsvAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authResult = Authorize(LoadIxPermissions.FinancePayrollView);
        if (authResult.IsFailure)
        {
            return Result<byte[]>.Failure(authResult.Error!);
        }

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Lines)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (batch is null)
        {
            return Result<byte[]>.Failure(new DomainError("payroll_batch.not_found", "Payroll batch not found."));
        }

        var content = PastelPayrollBatchFormatter.FormatGenericCsv(batch);

        await auditWriter.WriteAsync(
            "Finance.PayrollBatch.ExportFormat",
            "Succeeded",
            "PayrollBatch",
            id.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                format = "generic_csv",
                byteCount = content.Length,
            }));

        return Result<byte[]>.Success(content);
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

    private static string GetTransactionCode(DriverDeductionCategory category) =>
        category switch
        {
            DriverDeductionCategory.TrafficFine => "FINE",
            DriverDeductionCategory.VehicleDamage => "DMG",
            DriverDeductionCategory.DieselLoss => "FUEL",
            DriverDeductionCategory.LoanRepayment => "LOAN",
            DriverDeductionCategory.Cellphone => "CELL",
            DriverDeductionCategory.Uniform => "UNIF",
            _ => "DED01",
        };

    private static PayrollBatchDetail MapBatch(PayrollBatch batch) =>
        new(
            batch.Id,
            batch.TenantId.Value,
            batch.BatchNumber,
            batch.PeriodStartDate,
            batch.PeriodEndDate,
            batch.Frequency,
            batch.CurrencyCode,
            batch.Status,
            batch.TotalDeductionsAmount,
            batch.CreatedAtUtc,
            batch.ApprovedAtUtc,
            batch.ApprovedByUserId,
            batch.ExportedAtUtc,
            batch.ExportedByUserId,
            batch.Lines.Select(l => new PayrollBatchLineDetail(
                l.Id,
                l.DriverId,
                l.DriverEmployeeNumber,
                l.DriverName,
                l.DeductionId,
                l.TransactionCode,
                l.Description,
                l.Amount,
                l.BalanceAfterDeduction)).ToList());
}
