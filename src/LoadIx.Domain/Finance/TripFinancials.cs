using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Trips;

namespace LoadIx.Domain.Finance;

public sealed class TripFinancials : AggregateRoot<Guid>, ITenantOwned
{
    private readonly List<CostEntry> _costs = [];

    private TripFinancials()
    {
        TripNumber = string.Empty;
        CustomerName = string.Empty;
        ContractNumber = string.Empty;
        CurrencyCode = string.Empty;
        TaxTreatment = TaxTreatment.NotReviewed;
        CalculationVersion = string.Empty;
    }

    private TripFinancials(
        Guid id,
        TenantId tenantId,
        Guid tripId,
        string tripNumber,
        Guid customerId,
        string customerName,
        Guid contractId,
        string contractNumber,
        TripRateSnapshot rateSnapshot,
        decimal billableTonnes,
        TaxTreatment taxTreatment,
        decimal taxRatePercent,
        DateTimeOffset calculatedAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        TripId = tripId;
        TripNumber = tripNumber.Trim().ToUpperInvariant();
        CustomerId = customerId;
        CustomerName = customerName.Trim();
        TransportContractId = contractId;
        ContractNumber = contractNumber.Trim().ToUpperInvariant();
        CurrencyCode = rateSnapshot.CurrencyCode;
        TaxTreatment = taxTreatment;
        TaxRatePercent = decimal.Round(taxRatePercent, 4, MidpointRounding.AwayFromZero);
        CalculatedAtUtc = calculatedAtUtc.ToUniversalTime();
        CalculationVersion = "trip-financials-v1";
        ApplyRevenue(rateSnapshot, billableTonnes);
    }

    public TenantId TenantId { get; private set; }
    public Guid TripId { get; private set; }
    public string TripNumber { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; }
    public Guid TransportContractId { get; private set; }
    public string ContractNumber { get; private set; }
    public Guid? InvoiceBatchId { get; private set; }
    public string CurrencyCode { get; private set; }
    public ContractRateType RateType { get; private set; }
    public Guid RateId { get; private set; }
    public decimal? FixedAmount { get; private set; }
    public decimal? BaseFee { get; private set; }
    public decimal? PerTonneAmount { get; private set; }
    public decimal? MinimumCharge { get; private set; }
    public decimal? FuelSurchargePercent { get; private set; }
    public decimal BillableTonnes { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Surcharge { get; private set; }
    public decimal TaxExclusiveAmount { get; private set; }
    public TaxTreatment TaxTreatment { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal Total { get; private set; }
    public DateTimeOffset CalculatedAtUtc { get; private set; }
    public string CalculationVersion { get; private set; }
    public FinancialApprovalStatus ApprovalStatus { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public string? ApprovalReason { get; private set; }

    public IReadOnlyCollection<CostEntry> Costs => _costs.AsReadOnly();
    public bool IsInvoiced => InvoiceBatchId.HasValue;
    public decimal TotalCost => decimal.Round(_costs.Sum(item => item.Amount), 4, MidpointRounding.AwayFromZero);
    public decimal GrossMargin => decimal.Round(TaxExclusiveAmount - TotalCost, 4, MidpointRounding.AwayFromZero);
    public decimal MarginPercentage => TaxExclusiveAmount == 0 ? 0 : decimal.Round(GrossMargin / TaxExclusiveAmount * 100m, 4, MidpointRounding.AwayFromZero);
    public decimal CostPerTonne => BillableTonnes == 0 ? 0 : decimal.Round(TotalCost / BillableTonnes, 4, MidpointRounding.AwayFromZero);
    public decimal RevenuePerTonne => BillableTonnes == 0 ? 0 : decimal.Round(TaxExclusiveAmount / BillableTonnes, 4, MidpointRounding.AwayFromZero);

    public static Result<TripFinancials> Create(
        Guid id,
        TenantId tenantId,
        Guid tripId,
        string tripNumber,
        Guid customerId,
        string customerName,
        Guid contractId,
        string contractNumber,
        TripRateSnapshot rateSnapshot,
        decimal billableTonnes,
        TaxTreatment taxTreatment,
        decimal taxRatePercent,
        DateTimeOffset calculatedAtUtc)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || tripId == Guid.Empty
            || customerId == Guid.Empty || contractId == Guid.Empty
            || calculatedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result<TripFinancials>.Failure(new DomainError(
                "finance.context_invalid",
                "Trip financials require tenant, trip, customer, contract, and UTC timestamp."));
        }

        var error = DomainValidation.Required(tripNumber, "finance.trip_number_required", 80)
            ?? DomainValidation.Required(customerName, "finance.customer_name_required", 200)
            ?? DomainValidation.Required(contractNumber, "finance.contract_number_required", 80);
        if (error is not null) return Result<TripFinancials>.Failure(error);
        if (!Enum.IsDefined(taxTreatment) || taxRatePercent < 0 || taxRatePercent > 100)
        {
            return Result<TripFinancials>.Failure(new DomainError(
                "finance.tax_invalid",
                "Tax treatment and rate must be explicit and valid."));
        }

        if (rateSnapshot.RateType != ContractRateType.FixedPerTrip && billableTonnes <= 0)
        {
            return Result<TripFinancials>.Failure(new DomainError(
                "finance.billable_weight_required",
                "A per-tonne trip requires a positive reconciled billable weight."));
        }

        return Result<TripFinancials>.Success(new TripFinancials(
            id,
            tenantId,
            tripId,
            tripNumber,
            customerId,
            customerName,
            contractId,
            contractNumber,
            rateSnapshot,
            billableTonnes,
            taxTreatment,
            taxRatePercent,
            calculatedAtUtc));
    }

    public Result AddCost(CostEntry entry)
    {
        if (ApprovalStatus == FinancialApprovalStatus.Approved || IsInvoiced)
        {
            return Result.Failure(new DomainError(
                "finance.cost_locked",
                "Approved or invoiced financial records require an adjustment entry."));
        }

        return AddCostEntry(entry);
    }

    public Result AddAdjustment(CostEntry entry) => AddCostEntry(entry);

    public Result Approve(Guid actorUserId, DateTimeOffset approvedAtUtc, string? reason)
    {
        if (actorUserId == Guid.Empty || approvedAtUtc.Offset != TimeSpan.Zero)
        {
            return Result.Failure(new DomainError(
                "finance.approval_context_invalid",
                "Financial approval requires an actor and UTC timestamp."));
        }

        if (TaxTreatment == TaxTreatment.NotReviewed)
        {
            return Result.Failure(new DomainError(
                "finance.tax_not_reviewed",
                "Tax treatment must be reviewed before approval."));
        }

        ApprovalStatus = FinancialApprovalStatus.Approved;
        ApprovedByUserId = actorUserId;
        ApprovedAtUtc = approvedAtUtc.ToUniversalTime();
        ApprovalReason = reason?.Trim();
        return Result.Success();
    }

    public Result AttachInvoiceBatch(Guid invoiceBatchId)
    {
        if (invoiceBatchId == Guid.Empty || InvoiceBatchId.HasValue)
        {
            return Result.Failure(new DomainError(
                "finance.duplicate_invoice",
                "The trip is already assigned to an invoice batch."));
        }

        if (ApprovalStatus != FinancialApprovalStatus.Approved)
        {
            return Result.Failure(new DomainError(
                "finance.not_approved",
                "Only approved financials can be invoiced."));
        }

        InvoiceBatchId = invoiceBatchId;
        return Result.Success();
    }

    private Result AddCostEntry(CostEntry entry)
    {
        if (entry.TenantId != TenantId || entry.TripFinancialsId != Id)
        {
            return Result.Failure(new DomainError(
                "finance.cost_scope_invalid",
                "Cost entries must belong to the same tenant and financial record."));
        }

        if (entry.CurrencyCode != CurrencyCode)
        {
            return Result.Failure(new DomainError(
                "finance.currency_mismatch",
                "Cost currency must match the trip revenue currency."));
        }

        _costs.Add(entry);
        return Result.Success();
    }

    private void ApplyRevenue(TripRateSnapshot rateSnapshot, decimal billableTonnes)
    {
        RateId = rateSnapshot.RateId;
        RateType = rateSnapshot.RateType;
        FixedAmount = rateSnapshot.FixedAmount;
        BaseFee = rateSnapshot.BaseFee;
        PerTonneAmount = rateSnapshot.PerTonneAmount;
        MinimumCharge = rateSnapshot.MinimumCharge;
        FuelSurchargePercent = rateSnapshot.FuelSurchargePercent;
        BillableTonnes = decimal.Round(billableTonnes, 3, MidpointRounding.AwayFromZero);

        var revenueBase = rateSnapshot.RateType == ContractRateType.FixedPerTrip
            ? rateSnapshot.FixedAmount!.Value
            : (rateSnapshot.BaseFee ?? 0m) + BillableTonnes * rateSnapshot.PerTonneAmount!.Value;
        Subtotal = decimal.Round(revenueBase, 4, MidpointRounding.AwayFromZero);
        Surcharge = rateSnapshot.FuelSurchargePercent > 0
            ? decimal.Round(Subtotal * rateSnapshot.FuelSurchargePercent.Value / 100m, 4, MidpointRounding.AwayFromZero)
            : 0m;
        TaxExclusiveAmount = Subtotal + Surcharge;
        if (rateSnapshot.MinimumCharge is not null
            && TaxExclusiveAmount < rateSnapshot.MinimumCharge.Value)
        {
            TaxExclusiveAmount = rateSnapshot.MinimumCharge.Value;
        }

        TaxExclusiveAmount = decimal.Round(
            TaxExclusiveAmount,
            rateSnapshot.RoundingDecimalPlaces,
            rateSnapshot.RoundingMode);
        TaxAmount = TaxTreatment == TaxTreatment.StandardRated
            ? decimal.Round(TaxExclusiveAmount * TaxRatePercent / 100m, 4, MidpointRounding.AwayFromZero)
            : 0m;
        Total = decimal.Round(TaxExclusiveAmount + TaxAmount, 4, MidpointRounding.AwayFromZero);
    }
}
