namespace LoadIx.Domain.Common;

public interface ITenantOwned
{
    TenantId TenantId { get; }
}
