using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Identity;

public sealed class UserInvitation
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public string RoleIds { get; set; } = string.Empty;

    public Guid InvitedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
