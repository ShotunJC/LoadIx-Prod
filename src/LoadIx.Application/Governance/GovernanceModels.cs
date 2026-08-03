namespace LoadIx.Application.Governance;

public sealed record RetentionPolicyModel(
    Guid? Id,
    int OperationalDataRetentionDays,
    int AuditRetentionDays,
    int DocumentMetadataRetentionDays,
    bool LegalHold,
    string? LegalHoldReason,
    string Version);

public sealed record RetentionPolicyUpdateModel(
    int OperationalDataRetentionDays,
    int AuditRetentionDays,
    int DocumentMetadataRetentionDays,
    bool LegalHold,
    string? LegalHoldReason,
    string? Version = null);

public sealed record PersonalDataInventoryItem(
    string DataClass,
    string Examples,
    string Purpose,
    string Storage,
    string DefaultRetention,
    string AnonymisationApproach);

public sealed record AnonymisationRequestModel(
    Guid UserId,
    string Reason);

public sealed record AnonymisationApprovalModel(
    string ReviewReason);

public sealed record AnonymisationRequestResponse(
    Guid Id,
    Guid UserId,
    string Status,
    string Reason,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAtUtc,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    DateTimeOffset? CompletedAtUtc,
    string Version);

public sealed record TenantExportResult(
    byte[] Content,
    string FileName);

public enum GovernanceFailure
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

#pragma warning disable CA1000
public sealed record GovernanceResult<T>(
    T? Value,
    GovernanceFailure Failure,
    string Code,
    string Message)
{
    public bool IsSuccess => Failure == GovernanceFailure.None;

    public static GovernanceResult<T> Success(T value) =>
        new(value, GovernanceFailure.None, string.Empty, string.Empty);

    public static GovernanceResult<T> Fail(
        GovernanceFailure failure,
        string code,
        string message) =>
        new(default, failure, code, message);
}
#pragma warning restore CA1000

public interface IDataGovernanceService
{
    Task<GovernanceResult<RetentionPolicyModel>> GetRetentionPolicyAsync(
        CancellationToken cancellationToken);

    Task<GovernanceResult<RetentionPolicyModel>> UpdateRetentionPolicyAsync(
        RetentionPolicyUpdateModel model,
        CancellationToken cancellationToken);

    IReadOnlyList<PersonalDataInventoryItem> GetPersonalDataInventory();

    Task<GovernanceResult<TenantExportResult>> ExportTenantDataAsync(
        CancellationToken cancellationToken);

    Task<GovernanceResult<AnonymisationRequestResponse>> RequestAnonymisationAsync(
        AnonymisationRequestModel model,
        CancellationToken cancellationToken);

    Task<GovernanceResult<IReadOnlyList<AnonymisationRequestResponse>>> ListAnonymisationRequestsAsync(
        CancellationToken cancellationToken);

    Task<GovernanceResult<AnonymisationRequestResponse>> ApproveAnonymisationAsync(
        Guid requestId,
        AnonymisationApprovalModel model,
        CancellationToken cancellationToken);
}
