using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Governance;

public sealed class AnonymisationRequest : AuditableEntity<Guid>, ITenantOwned
{
    private AnonymisationRequest(
        Guid id,
        TenantId tenantId,
        Guid userId,
        string reason,
        Guid requestedByUserId,
        DateTimeOffset requestedAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Reason = reason.Trim();
        RequestedByUserId = requestedByUserId;
        RequestedAtUtc = requestedAtUtc.ToUniversalTime();
        Status = AnonymisationRequestStatus.Pending;
    }

    private AnonymisationRequest()
    {
        Reason = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public AnonymisationRequestStatus Status { get; private set; }

    public string Reason { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAtUtc { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public string? ReviewReason { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static AnonymisationRequest Create(
        Guid id,
        TenantId tenantId,
        Guid userId,
        string reason,
        Guid requestedByUserId,
        DateTimeOffset requestedAtUtc) =>
        new(id, tenantId, userId, reason, requestedByUserId, requestedAtUtc);

    public void Complete(
        Guid reviewedByUserId,
        DateTimeOffset reviewedAtUtc,
        string reviewReason)
    {
        Status = AnonymisationRequestStatus.Completed;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc.ToUniversalTime();
        ReviewReason = reviewReason.Trim();
        CompletedAtUtc = reviewedAtUtc.ToUniversalTime();
    }
}

public enum AnonymisationRequestStatus
{
    Pending,
    Completed,
}
