using LoadIx.Domain.Common;

namespace LoadIx.Application.Abstractions;

public interface ICurrentRequestContext
{
    TenantId? TenantId { get; }

    Guid? UserId { get; }

    bool IsSupportContext { get; }

    bool HasPermission(string permission);
}
