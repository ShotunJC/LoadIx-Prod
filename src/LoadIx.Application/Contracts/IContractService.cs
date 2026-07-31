namespace LoadIx.Application.Contracts;

public interface IContractService
{
    Task<ContractResult<ContractPagedResponse<ContractListItem>>> ListAsync(
        ContractQuery query,
        CancellationToken cancellationToken);

    Task<ContractResult<ContractDetail>> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ContractResult<ContractDetail>> CreateAsync(
        ContractWriteModel model,
        CancellationToken cancellationToken);

    Task<ContractResult<ContractDetail>> UpdateAsync(
        Guid id,
        ContractWriteModel model,
        CancellationToken cancellationToken);

    Task<ContractResult<ContractDetail>> ActivateAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken);

    Task<ContractResult<ContractDetail>> SuspendAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken);

    Task<ContractResult<RateCalculationResponse>> PreviewRateAsync(
        Guid id,
        RatePreviewModel model,
        CancellationToken cancellationToken);

    Task<ContractResult<IReadOnlyList<ContractHistoryItem>>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken);
}
