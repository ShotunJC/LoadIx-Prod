namespace LoadIx.Domain.Finance;

public enum CostEntryType
{
    DriverCost,
    FuelEstimate,
    FuelActual,
    Toll,
    Subcontractor,
    WeighbridgeFee,
    LoadingFee,
    UnloadingFee,
    MiscellaneousApprovedCost,
}

public enum TaxTreatment
{
    NotReviewed,
    OutOfScope,
    Exempt,
    ZeroRated,
    StandardRated,
}

public enum FinancialApprovalStatus
{
    Pending,
    Approved,
    Rejected,
}

public enum InvoiceBatchStatus
{
    Draft,
    Approved,
    Exported,
    Cancelled,
}
