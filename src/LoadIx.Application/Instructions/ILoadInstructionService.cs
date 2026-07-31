namespace LoadIx.Application.Instructions;

public interface ILoadInstructionService
{
    Task<InstructionResult<InstructionPagedResponse<InstructionListItem>>> ListAsync(
        InstructionQuery query,
        CancellationToken cancellationToken);

    Task<InstructionResult<InstructionDetail>> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<InstructionResult<InstructionDetail>> CreateAsync(
        InstructionWriteModel model,
        CancellationToken cancellationToken);

    Task<InstructionResult<InstructionDetail>> UpdateAsync(
        Guid id,
        InstructionWriteModel model,
        CancellationToken cancellationToken);

    Task<InstructionResult<InstructionDetail>> PlanAsync(
        Guid id,
        string version,
        CancellationToken cancellationToken);

    Task<InstructionResult<InstructionDetail>> CancelAsync(
        Guid id,
        InstructionCancellationModel model,
        CancellationToken cancellationToken);

    Task<InstructionResult<InstructionDetail>> GenerateTripsAsync(
        Guid id,
        GenerateTripsModel model,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<InstructionResult<IReadOnlyList<InstructionHistoryItem>>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken);
}
