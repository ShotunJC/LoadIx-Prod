using LoadIx.Domain.Common;

namespace LoadIx.Domain.Finance;

public sealed class InvoiceBatch : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<InvoiceBatchLine> _lines = [];

    private InvoiceBatch()
    {
        BatchNumber = string.Empty;
        CustomerName = string.Empty;
        ContractNumber = string.Empty;
        CurrencyCode = string.Empty;
    }

    private InvoiceBatch(
        Guid id,
        TenantId tenantId,
        string batchNumber,
        Guid customerId,
        string customerName,
        Guid contractId,
        string contractNumber,
        string currencyCode,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        BatchNumber = batchNumber.Trim().ToUpperInvariant();
        CustomerId = customerId;
        CustomerName = customerName.Trim();
        TransportContractId = contractId;
        ContractNumber = contractNumber.Trim().ToUpperInvariant();
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Status = InvoiceBatchStatus.Draft;
    }

    public TenantId TenantId { get; private set; }
    public string BatchNumber { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; }
    public Guid TransportContractId { get; private set; }
    public string ContractNumber { get; private set; }
    public string CurrencyCode { get; private set; }
    public InvoiceBatchStatus Status { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ExportedAtUtc { get; private set; }
    public Guid? ExportedByUserId { get; private set; }
    public IReadOnlyCollection<InvoiceBatchLine> Lines => _lines.AsReadOnly();
    public decimal TaxExclusiveAmount => _lines.Sum(line => line.TaxExclusiveAmount);
    public decimal TaxAmount => _lines.Sum(line => line.TaxAmount);
    public decimal Total => _lines.Sum(line => line.Total);

    public static Result<InvoiceBatch> Create(
        Guid id,
        TenantId tenantId,
        string batchNumber,
        Guid customerId,
        string customerName,
        Guid contractId,
        string contractNumber,
        string currencyCode,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || customerId == Guid.Empty
            || contractId == Guid.Empty || createdAtUtc.Offset != TimeSpan.Zero)
        {
            return Result<InvoiceBatch>.Failure(new DomainError(
                "invoice.context_invalid",
                "Invoice batches require tenant, customer, contract, and UTC timestamp."));
        }

        var money = Money.Create(0m, currencyCode);
        var error = money.IsFailure
            ? money.Error
            : DomainValidation.Required(batchNumber, "invoice.batch_number_required", 80)
                ?? DomainValidation.Required(customerName, "invoice.customer_name_required", 200)
                ?? DomainValidation.Required(contractNumber, "invoice.contract_number_required", 80);
        return error is null
            ? Result<InvoiceBatch>.Success(new InvoiceBatch(
                id,
                tenantId,
                batchNumber,
                customerId,
                customerName,
                contractId,
                contractNumber,
                money.Value.CurrencyCode,
                createdAtUtc))
            : Result<InvoiceBatch>.Failure(error);
    }

    public Result AddLine(InvoiceBatchLine line)
    {
        if (Status != InvoiceBatchStatus.Draft)
        {
            return Result.Failure(new DomainError(
                "invoice.batch_locked",
                "Only draft invoice batches can receive lines."));
        }

        if (line.TenantId != TenantId || line.InvoiceBatchId != Id
            || line.CustomerId != CustomerId || line.TransportContractId != TransportContractId
            || line.CurrencyCode != CurrencyCode
            || _lines.Any(item => item.TripFinancialsId == line.TripFinancialsId))
        {
            return Result.Failure(new DomainError(
                "invoice.line_invalid",
                "Invoice lines must match the batch scope and not duplicate trips."));
        }

        _lines.Add(line);
        return Result.Success();
    }

    public Result Approve(Guid actorUserId, DateTimeOffset approvedAtUtc)
    {
        if (actorUserId == Guid.Empty || approvedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "invoice.approval_context_invalid",
                "Invoice approval requires an actor and UTC timestamp."));
        }

        if (Status != InvoiceBatchStatus.Draft || _lines.Count == 0)
        {
            return Result.Failure(new DomainError(
                "invoice.approval_invalid",
                "Only non-empty draft invoice batches can be approved."));
        }

        Status = InvoiceBatchStatus.Approved;
        ApprovedByUserId = actorUserId;
        ApprovedAtUtc = approvedAtUtc.ToUniversalTime();
        return Result.Success();
    }

    public Result MarkExported(Guid actorUserId, DateTimeOffset exportedAtUtc)
    {
        if (actorUserId == Guid.Empty || exportedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "invoice.export_context_invalid",
                "Invoice export requires an actor and UTC timestamp."));
        }

        if (Status != InvoiceBatchStatus.Approved)
        {
            return Result.Failure(new DomainError(
                "invoice.export_invalid",
                "Only approved invoice batches can be marked exported."));
        }

        Status = InvoiceBatchStatus.Exported;
        ExportedByUserId = actorUserId;
        ExportedAtUtc = exportedAtUtc.ToUniversalTime();
        return Result.Success();
    }
}
