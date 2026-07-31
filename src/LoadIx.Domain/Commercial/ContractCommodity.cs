using LoadIx.Domain.Common;

namespace LoadIx.Domain.Commercial;

public sealed class ContractCommodity : Entity<Guid>, ITenantOwned
{
    private ContractCommodity(Guid id, TenantId tenantId, Guid commodityId)
        : base(id)
    {
        TenantId = tenantId;
        CommodityId = commodityId;
    }

    private ContractCommodity()
    {
    }

    public TenantId TenantId { get; private set; }

    public Guid TransportContractId { get; private set; }

    public Guid CommodityId { get; private set; }

    internal static Result<ContractCommodity> Create(
        Guid id,
        TenantId tenantId,
        Guid commodityId)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || commodityId == Guid.Empty)
        {
            return Result<ContractCommodity>.Failure(new DomainError(
                "contract.commodity_identifiers_required",
                "Contract commodity identifiers are required."));
        }

        return Result<ContractCommodity>.Success(
            new ContractCommodity(id, tenantId, commodityId));
    }
}
