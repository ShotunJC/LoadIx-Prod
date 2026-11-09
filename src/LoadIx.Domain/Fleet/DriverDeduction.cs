using LoadIx.Domain.Common;

namespace LoadIx.Domain.Fleet;

public sealed class DriverDeduction : AggregateRoot<Guid>, ITenantOwned
{
    private DriverDeduction(
        Guid id,
        TenantId tenantId,
        Guid driverId,
        string deductionNumber,
        DriverDeductionCategory category,
        string currencyCode,
        decimal totalAmount,
        decimal installmentAmount,
        DateOnly effectiveDate,
        string description,
        DateTimeOffset createdAtUtc,
        Guid? warningId,
        Guid? tripId,
        Guid? consentDocumentId)
        : base(id)
    {
        TenantId = tenantId;
        DriverId = driverId;
        DeductionNumber = deductionNumber.Trim().ToUpperInvariant();
        Category = category;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        TotalAmount = totalAmount;
        InstallmentAmount = installmentAmount;
        RemainingBalance = totalAmount;
        EffectiveDate = effectiveDate;
        Description = description.Trim();
        WarningId = warningId;
        TripId = tripId;
        ConsentDocumentId = consentDocumentId;
        Status = DriverDeductionStatus.Draft;
    }

    private DriverDeduction()
    {
        DeductionNumber = string.Empty;
        CurrencyCode = string.Empty;
        Description = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid DriverId { get; private set; }

    public string DeductionNumber { get; private set; }

    public DriverDeductionCategory Category { get; private set; }

    public string CurrencyCode { get; private set; }

    public decimal TotalAmount { get; private set; }

    public decimal InstallmentAmount { get; private set; }

    public decimal RemainingBalance { get; private set; }

    public DateOnly EffectiveDate { get; private set; }

    public string Description { get; private set; }

    public Guid? WarningId { get; private set; }

    public Guid? TripId { get; private set; }

    public Guid? ConsentDocumentId { get; private set; }

    public DriverDeductionStatus Status { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public static Result<DriverDeduction> Create(
        Guid id,
        TenantId tenantId,
        Guid driverId,
        string deductionNumber,
        DriverDeductionCategory category,
        string currencyCode,
        decimal totalAmount,
        decimal installmentAmount,
        DateOnly effectiveDate,
        string description,
        DateTimeOffset createdAtUtc,
        Guid? warningId = null,
        Guid? tripId = null,
        Guid? consentDocumentId = null)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || driverId == Guid.Empty)
        {
            return Result<DriverDeduction>.Failure(new DomainError(
                "driver_deduction.identifiers_required",
                "Deduction ID, Tenant ID, and Driver ID are required."));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            return Result<DriverDeduction>.Failure(new DomainError(
                "driver_deduction.created_utc_required",
                "Creation timestamp must be UTC."));
        }

        var money = Money.Create(totalAmount, currencyCode);
        if (money.IsFailure)
        {
            return Result<DriverDeduction>.Failure(money.Error!);
        }

        if (totalAmount <= 0m)
        {
            return Result<DriverDeduction>.Failure(new DomainError(
                "driver_deduction.total_must_be_positive",
                "Total deduction amount must be greater than zero."));
        }

        if (installmentAmount <= 0m || installmentAmount > totalAmount)
        {
            return Result<DriverDeduction>.Failure(new DomainError(
                "driver_deduction.invalid_installment",
                "Installment amount must be greater than zero and cannot exceed total deduction amount."));
        }

        var error = DomainValidation.Required(deductionNumber, "driver_deduction.deduction_number", 60)
            ?? DomainValidation.Required(description, "driver_deduction.description", 1000);

        if (error is not null)
        {
            return Result<DriverDeduction>.Failure(error);
        }

        if (!Enum.IsDefined(category))
        {
            return Result<DriverDeduction>.Failure(new DomainError(
                "driver_deduction.invalid_category",
                "Unsupported driver deduction category."));
        }

        return Result<DriverDeduction>.Success(new DriverDeduction(
            id,
            tenantId,
            driverId,
            deductionNumber,
            category,
            money.Value.CurrencyCode,
            totalAmount,
            installmentAmount,
            effectiveDate,
            description,
            createdAtUtc,
            warningId,
            tripId,
            consentDocumentId));
    }

    public Result Approve(Guid approvedByUserId, DateTimeOffset approvedAtUtc)
    {
        if (approvedByUserId == Guid.Empty || approvedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "driver_deduction.approval_context_invalid",
                "Approval requires an actor and UTC timestamp."));
        }

        if (Status != DriverDeductionStatus.Draft)
        {
            return Result.Failure(new DomainError(
                "driver_deduction.cannot_approve",
                "Only draft deductions can be approved."));
        }

        Status = DriverDeductionStatus.Active;
        ApprovedByUserId = approvedByUserId;
        ApprovedAtUtc = approvedAtUtc;
        return Result.Success();
    }

    public Result ApplyInstallment(decimal amount)
    {
        if (Status != DriverDeductionStatus.Active)
        {
            return Result.Failure(new DomainError(
                "driver_deduction.not_active",
                "Only active deductions can have installments applied."));
        }

        if (amount <= 0m)
        {
            return Result.Failure(new DomainError(
                "driver_deduction.installment_amount_invalid",
                "Applied installment must be greater than zero."));
        }

        RemainingBalance = Math.Max(0m, RemainingBalance - amount);
        if (RemainingBalance == 0m)
        {
            Status = DriverDeductionStatus.Completed;
        }

        return Result.Success();
    }

    public Result Cancel(string reason, Guid cancelledByUserId, DateTimeOffset cancelledAtUtc)
    {
        if (cancelledByUserId == Guid.Empty || cancelledAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "driver_deduction.cancel_context_invalid",
                "Cancellation requires an actor and UTC timestamp."));
        }

        if (Status == DriverDeductionStatus.Completed || Status == DriverDeductionStatus.Cancelled)
        {
            return Result.Failure(new DomainError(
                "driver_deduction.cannot_cancel",
                "Completed or cancelled deductions cannot be cancelled."));
        }

        var error = DomainValidation.Required(reason, "driver_deduction.cancellation_reason", 500);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        Status = DriverDeductionStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledByUserId = cancelledByUserId;
        CancelledAtUtc = cancelledAtUtc;
        return Result.Success();
    }
}
