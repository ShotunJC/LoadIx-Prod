using LoadIx.Domain.Common;

namespace LoadIx.Domain.Finance;

public sealed class PayrollBatch : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<PayrollBatchLine> _lines = [];

    private PayrollBatch(
        Guid id,
        TenantId tenantId,
        string batchNumber,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        PayrollFrequency frequency,
        string currencyCode,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        BatchNumber = batchNumber.Trim().ToUpperInvariant();
        PeriodStartDate = periodStartDate;
        PeriodEndDate = periodEndDate;
        Frequency = frequency;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Status = PayrollBatchStatus.Draft;
    }

    private PayrollBatch()
    {
        BatchNumber = string.Empty;
        CurrencyCode = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public string BatchNumber { get; private set; }

    public DateOnly PeriodStartDate { get; private set; }

    public DateOnly PeriodEndDate { get; private set; }

    public PayrollFrequency Frequency { get; private set; }

    public string CurrencyCode { get; private set; }

    public PayrollBatchStatus Status { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ExportedAtUtc { get; private set; }

    public Guid? ExportedByUserId { get; private set; }

    public IReadOnlyCollection<PayrollBatchLine> Lines => _lines.AsReadOnly();

    public decimal TotalDeductionsAmount => _lines.Sum(line => line.Amount);

    public static Result<PayrollBatch> Create(
        Guid id,
        TenantId tenantId,
        string batchNumber,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        PayrollFrequency frequency,
        string currencyCode,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || tenantId.IsEmpty)
        {
            return Result<PayrollBatch>.Failure(new DomainError(
                "payroll_batch.identifiers_required",
                "Batch ID and Tenant ID are required."));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            return Result<PayrollBatch>.Failure(new DomainError(
                "payroll_batch.created_utc_required",
                "Creation timestamp must be UTC."));
        }

        if (periodEndDate < periodStartDate)
        {
            return Result<PayrollBatch>.Failure(new DomainError(
                "payroll_batch.invalid_dates",
                "Period end date cannot precede period start date."));
        }

        var money = Money.Create(0m, currencyCode);
        if (money.IsFailure)
        {
            return Result<PayrollBatch>.Failure(money.Error!);
        }

        var error = DomainValidation.Required(batchNumber, "payroll_batch.batch_number", 60);
        if (error is not null)
        {
            return Result<PayrollBatch>.Failure(error);
        }

        if (!Enum.IsDefined(frequency))
        {
            return Result<PayrollBatch>.Failure(new DomainError(
                "payroll_batch.invalid_frequency",
                "Unsupported payroll frequency."));
        }

        return Result<PayrollBatch>.Success(new PayrollBatch(
            id,
            tenantId,
            batchNumber,
            periodStartDate,
            periodEndDate,
            frequency,
            money.Value.CurrencyCode,
            createdAtUtc));
    }

    public Result AddLine(PayrollBatchLine line)
    {
        if (Status != PayrollBatchStatus.Draft)
        {
            return Result.Failure(new DomainError(
                "payroll_batch.not_draft",
                "Lines can only be added to a draft payroll batch."));
        }

        if (line.TenantId != TenantId || line.PayrollBatchId != Id)
        {
            return Result.Failure(new DomainError(
                "payroll_batch.tenant_mismatch",
                "Line must belong to the same tenant and batch."));
        }

        if (line.DeductionId.HasValue && _lines.Any(l => l.DeductionId == line.DeductionId))
        {
            return Result.Failure(new DomainError(
                "payroll_batch.duplicate_deduction",
                "A deduction can only be included once per payroll batch."));
        }

        _lines.Add(line);
        return Result.Success();
    }

    public Result Approve(Guid actorUserId, DateTimeOffset approvedAtUtc)
    {
        if (actorUserId == Guid.Empty || approvedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "payroll_batch.approval_context_invalid",
                "Approval requires an actor and UTC timestamp."));
        }

        if (Status != PayrollBatchStatus.Draft)
        {
            return Result.Failure(new DomainError(
                "payroll_batch.cannot_approve",
                "Only draft payroll batches can be approved."));
        }

        if (_lines.Count == 0)
        {
            return Result.Failure(new DomainError(
                "payroll_batch.no_lines",
                "Cannot approve an empty payroll batch."));
        }

        Status = PayrollBatchStatus.Approved;
        ApprovedByUserId = actorUserId;
        ApprovedAtUtc = approvedAtUtc;
        return Result.Success();
    }

    public Result MarkExported(Guid actorUserId, DateTimeOffset exportedAtUtc)
    {
        if (actorUserId == Guid.Empty || exportedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "payroll_batch.export_context_invalid",
                "Export marking requires an actor and UTC timestamp."));
        }

        if (Status != PayrollBatchStatus.Approved)
        {
            return Result.Failure(new DomainError(
                "payroll_batch.cannot_export",
                "Only approved payroll batches can be marked as exported."));
        }

        Status = PayrollBatchStatus.Exported;
        ExportedByUserId = actorUserId;
        ExportedAtUtc = exportedAtUtc;
        return Result.Success();
    }
}
