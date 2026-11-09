using LoadIx.Domain.Common;

namespace LoadIx.Domain.Finance;

public sealed class PayrollBatchLine : Entity<Guid>, ITenantOwned
{
    private PayrollBatchLine(
        Guid id,
        TenantId tenantId,
        Guid payrollBatchId,
        Guid driverId,
        string? driverEmployeeNumber,
        string driverName,
        Guid? deductionId,
        string transactionCode,
        string description,
        decimal amount,
        decimal balanceAfterDeduction)
        : base(id)
    {
        TenantId = tenantId;
        PayrollBatchId = payrollBatchId;
        DriverId = driverId;
        DriverEmployeeNumber = string.IsNullOrWhiteSpace(driverEmployeeNumber) ? null : driverEmployeeNumber.Trim().ToUpperInvariant();
        DriverName = driverName.Trim();
        DeductionId = deductionId;
        TransactionCode = transactionCode.Trim().ToUpperInvariant();
        Description = description.Trim();
        Amount = amount;
        BalanceAfterDeduction = balanceAfterDeduction;
    }

    private PayrollBatchLine()
    {
        DriverName = string.Empty;
        TransactionCode = string.Empty;
        Description = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid PayrollBatchId { get; private set; }

    public Guid DriverId { get; private set; }

    public string? DriverEmployeeNumber { get; private set; }

    public string DriverName { get; private set; }

    public Guid? DeductionId { get; private set; }

    public string TransactionCode { get; private set; }

    public string Description { get; private set; }

    public decimal Amount { get; private set; }

    public decimal BalanceAfterDeduction { get; private set; }

    public static Result<PayrollBatchLine> Create(
        Guid id,
        TenantId tenantId,
        Guid payrollBatchId,
        Guid driverId,
        string? driverEmployeeNumber,
        string driverName,
        Guid? deductionId,
        string transactionCode,
        string description,
        decimal amount,
        decimal balanceAfterDeduction)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || payrollBatchId == Guid.Empty || driverId == Guid.Empty)
        {
            return Result<PayrollBatchLine>.Failure(new DomainError(
                "payroll_line.identifiers_required",
                "Line ID, Tenant ID, Batch ID, and Driver ID are required."));
        }

        if (amount <= 0m)
        {
            return Result<PayrollBatchLine>.Failure(new DomainError(
                "payroll_line.amount_must_be_positive",
                "Line amount must be greater than zero."));
        }

        if (balanceAfterDeduction < 0m)
        {
            return Result<PayrollBatchLine>.Failure(new DomainError(
                "payroll_line.invalid_balance",
                "Remaining balance after deduction cannot be negative."));
        }

        var error = DomainValidation.Required(driverName, "payroll_line.driver_name", 150)
            ?? DomainValidation.Required(transactionCode, "payroll_line.transaction_code", 30)
            ?? DomainValidation.Required(description, "payroll_line.description", 300);

        if (error is not null)
        {
            return Result<PayrollBatchLine>.Failure(error);
        }

        return Result<PayrollBatchLine>.Success(new PayrollBatchLine(
            id,
            tenantId,
            payrollBatchId,
            driverId,
            driverEmployeeNumber,
            driverName,
            deductionId,
            transactionCode,
            description,
            amount,
            balanceAfterDeduction));
    }
}
