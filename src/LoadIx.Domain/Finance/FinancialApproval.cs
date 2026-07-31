using LoadIx.Domain.Common;

namespace LoadIx.Domain.Finance;

public sealed class FinancialApproval : Entity<Guid>, ITenantOwned
{
    private FinancialApproval()
    {
        TargetType = string.Empty;
        Outcome = string.Empty;
        Reason = string.Empty;
    }

    private FinancialApproval(
        Guid id,
        TenantId tenantId,
        string targetType,
        Guid targetId,
        string outcome,
        Guid actorUserId,
        DateTimeOffset occurredAtUtc,
        string? reason)
        : base(id)
    {
        TenantId = tenantId;
        TargetType = targetType.Trim();
        TargetId = targetId;
        Outcome = outcome.Trim();
        ActorUserId = actorUserId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        Reason = reason?.Trim() ?? string.Empty;
    }

    public TenantId TenantId { get; private set; }
    public string TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string Outcome { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string Reason { get; private set; }

    public static Result<FinancialApproval> Create(
        Guid id,
        TenantId tenantId,
        string targetType,
        Guid targetId,
        string outcome,
        Guid actorUserId,
        DateTimeOffset occurredAtUtc,
        string? reason)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || targetId == Guid.Empty
            || actorUserId == Guid.Empty || occurredAtUtc.Offset != TimeSpan.Zero)
        {
            return Result<FinancialApproval>.Failure(new DomainError(
                "finance.approval_invalid",
                "Financial approval history requires tenant, target, actor, and UTC timestamp."));
        }

        var error = DomainValidation.Required(targetType, "finance.approval_target_required", 80)
            ?? DomainValidation.Required(outcome, "finance.approval_outcome_required", 40);
        return error is null
            ? Result<FinancialApproval>.Success(new FinancialApproval(
                id,
                tenantId,
                targetType,
                targetId,
                outcome,
                actorUserId,
                occurredAtUtc,
                reason))
            : Result<FinancialApproval>.Failure(error);
    }
}
