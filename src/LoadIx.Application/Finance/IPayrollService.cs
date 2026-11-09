using LoadIx.Domain.Common;

namespace LoadIx.Application.Finance;

public interface IPayrollService
{
    Task<Result<PayrollBatchPreview>> PreviewBatchAsync(
        PayrollBatchPreviewRequest request,
        CancellationToken cancellationToken);

    Task<Result<PayrollBatchDetail>> CreateBatchAsync(
        PayrollBatchCreateModel model,
        CancellationToken cancellationToken);

    Task<Result<PayrollBatchDetail>> GetBatchAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PayrollBatchListItem>>> ListBatchesAsync(
        CancellationToken cancellationToken);

    Task<Result<PayrollBatchDetail>> ApproveBatchAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Result<PayrollBatchDetail>> MarkExportedAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Result<byte[]>> ExportPastelPayrollCsvAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Result<byte[]>> ExportGenericPayrollCsvAsync(
        Guid id,
        CancellationToken cancellationToken);
}
