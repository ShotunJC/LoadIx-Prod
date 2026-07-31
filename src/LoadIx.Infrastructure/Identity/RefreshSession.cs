using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Identity;

public sealed class RefreshSession
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    public Guid UserId { get; set; }

    public TenantId? TenantId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public DateTimeOffset? ReuseDetectedAtUtc { get; set; }

    public string UserAgent { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public bool IsActive(DateTimeOffset now) =>
        RevokedAtUtc is null && ExpiresAtUtc > now;
}
