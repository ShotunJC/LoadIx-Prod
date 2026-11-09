namespace LoadIx.Application.Finance;

public interface IFinanceService
{
    Task<FinanceResult<TenantTaxConfigurationModel>> GetTaxConfigurationAsync(
        CancellationToken cancellationToken);

    Task<FinanceResult<TenantTaxConfigurationModel>> UpdateTaxConfigurationAsync(
        TenantTaxConfigurationWriteModel model,
        CancellationToken cancellationToken);

    Task<FinanceResult<TripFinancialsDetail>> GetTripFinancialsAsync(
        Guid tripId,
        CancellationToken cancellationToken);

    Task<FinanceResult<TripFinancialsDetail>> RecalculateTripFinancialsAsync(
        Guid tripId,
        CancellationToken cancellationToken);

    Task<FinanceResult<TripFinancialsDetail>> AddCostAsync(
        Guid tripId,
        CostEntryWriteModel model,
        CancellationToken cancellationToken);

    Task<FinanceResult<TripFinancialsDetail>> ApproveTripFinancialsAsync(
        Guid tripId,
        ApproveFinancialsModel model,
        CancellationToken cancellationToken);

    Task<FinanceResult<FinancePagedResponse<InvoiceReadyTrip>>> ListInvoiceReadyAsync(
        InvoiceReadyQuery query,
        CancellationToken cancellationToken);

    Task<FinanceResult<IReadOnlyList<InvoiceBatchPreview>>> PreviewInvoiceBatchAsync(
        InvoiceBatchPreviewRequest model,
        CancellationToken cancellationToken);

    Task<FinanceResult<InvoiceBatchDetail>> CreateInvoiceBatchAsync(
        InvoiceBatchCreateModel model,
        CancellationToken cancellationToken);

    Task<FinanceResult<FinancePagedResponse<InvoiceBatchListItem>>> ListInvoiceBatchesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<FinanceResult<InvoiceBatchDetail>> GetInvoiceBatchAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<FinanceResult<InvoiceBatchDetail>> ApproveInvoiceBatchAsync(
        Guid id,
        VersionedFinanceCommand model,
        CancellationToken cancellationToken);

    Task<FinanceResult<InvoiceBatchDetail>> MarkInvoiceBatchExportedAsync(
        Guid id,
        VersionedFinanceCommand model,
        CancellationToken cancellationToken);

    Task<FinanceResult<byte[]>> ExportInvoiceBatchCsvAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<FinanceResult<byte[]>> ExportInvoiceBatchPastelAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<FinanceResult<byte[]>> ExportInvoiceBatchPdfAsync(
        Guid id,
        CancellationToken cancellationToken);
}
