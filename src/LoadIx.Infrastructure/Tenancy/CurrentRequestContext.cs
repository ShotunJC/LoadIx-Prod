using LoadIx.Application.Abstractions;
using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Tenancy;

public sealed class CurrentRequestContext : ICurrentRequestContext
{
    private HashSet<string> _permissions = new(StringComparer.Ordinal);

    public TenantId? TenantId { get; private set; }

    public Guid? UserId { get; private set; }

    public bool IsSupportContext { get; private set; }

    public void Set(
        TenantId tenantId,
        Guid? userId,
        bool isSupportContext = false,
        IEnumerable<string>? permissions = null)
    {
        TenantId = tenantId;
        UserId = userId;
        IsSupportContext = isSupportContext;
        _permissions = permissions?.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
    }

    public void Clear()
    {
        TenantId = null;
        UserId = null;
        IsSupportContext = false;
        _permissions.Clear();
    }

    public bool HasPermission(string permission) => _permissions.Contains(permission);
}
