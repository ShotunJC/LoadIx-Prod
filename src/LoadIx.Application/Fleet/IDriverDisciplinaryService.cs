using LoadIx.Domain.Common;

namespace LoadIx.Application.Fleet;

public interface IDriverDisciplinaryService
{
    Task<Result<DriverWarningDetail>> IssueWarningAsync(
        Guid driverId,
        DriverWarningCreateModel model,
        CancellationToken cancellationToken);

    Task<Result<DriverWarningDetail>> RevokeWarningAsync(
        Guid warningId,
        RevokeWarningModel model,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DriverWarningDetail>>> GetDriverWarningsAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task<Result<DriverDeductionDetail>> CreateDeductionAsync(
        Guid driverId,
        DriverDeductionCreateModel model,
        CancellationToken cancellationToken);

    Task<Result<DriverDeductionDetail>> ApproveDeductionAsync(
        Guid deductionId,
        CancellationToken cancellationToken);

    Task<Result<DriverDeductionDetail>> CancelDeductionAsync(
        Guid deductionId,
        CancelDeductionModel model,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DriverDeductionDetail>>> GetDriverDeductionsAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task<Result<DriverDisciplinarySummary>> GetDriverDisciplinarySummaryAsync(
        Guid driverId,
        CancellationToken cancellationToken);
}
