using LoadIx.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace LoadIx.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public TenantId TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Permissions { get; set; } = string.Empty;

    public IReadOnlySet<string> GetPermissions() =>
        Permissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    public void SetPermissions(IEnumerable<string> permissions) =>
        Permissions = string.Join(
            ',',
            permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
