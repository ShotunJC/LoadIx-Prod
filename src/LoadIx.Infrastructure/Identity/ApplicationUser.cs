using LoadIx.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace LoadIx.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public TenantId? TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsPlatformAdministrator { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ModifiedAtUtc { get; set; }
}
