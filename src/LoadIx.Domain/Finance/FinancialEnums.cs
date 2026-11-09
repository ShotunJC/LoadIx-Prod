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

public enum PayrollFrequency
{
    Monthly = 1,
    Fortnightly = 2,
    Weekly = 3,
}

public enum PayrollBatchStatus
{
    Draft = 1,
    Approved = 2,
    Exported = 3,
    Cancelled = 4,
}

