using LoadIx.Domain.Common;

namespace LoadIx.Domain.Finance;

public sealed class InvoiceBatchLine : Entity<Guid>, ITenantOwned
{
    private InvoiceBatchLine()
    {
        TripNumber = string.Empty;
        CurrencyCode = string.Empty;
        TaxTreatment = TaxTreatment.NotReviewed;
    }

    private InvoiceBatchLine(Guid id, Guid invoiceBatchId, TripFinancials financials)
        : base(id)
    {
        TenantId = financials.TenantId;
        InvoiceBatchId = invoiceBatchId;
        TripFinancialsId = financials.Id;
        TripId = financials.TripId;
        TripNumber = financials.TripNumber;
        CustomerId = financials.CustomerId;
        TransportContractId = financials.TransportContractId;
        BillableTonnes = financials.BillableTonnes;
        CurrencyCode = financials.CurrencyCode;
        TaxExclusiveAmount = financials.TaxExclusiveAmount;
        TaxTreatment = financials.TaxTreatment;
        TaxAmount = financials.TaxAmount;
        Total = financials.Total;
    }

    public TenantId TenantId { get; private set; }
    public Guid InvoiceBatchId { get; private set; }
    public Guid TripFinancialsId { get; private set; }
    public Guid TripId { get; private set; }
    public string TripNumber { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid TransportContractId { get; private set; }
    public decimal BillableTonnes { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal TaxExclusiveAmount { get; private set; }
    public TaxTreatment TaxTreatment { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal Total { get; private set; }

    public static Result<InvoiceBatchLine> Create(
        Guid id,
        Guid invoiceBatchId,
        TripFinancials financials)
    {
        if (id == Guid.Empty || invoiceBatchId == Guid.Empty)
        {
            return Result<InvoiceBatchLine>.Failure(new DomainError(
                "invoice.line_context_invalid",
                "Invoice lines require identifiers."));
        }

        if (financials.ApprovalStatus != FinancialApprovalStatus.Approved
            || financials.IsInvoiced)
        {
            return Result<InvoiceBatchLine>.Failure(new DomainError(
                "invoice.trip_not_invoiceable",
                "Only approved, uninvoiced trip financials can be invoiced."));
        }

        return Result<InvoiceBatchLine>.Success(new InvoiceBatchLine(
            id,
            invoiceBatchId,
            financials));
    }
}
