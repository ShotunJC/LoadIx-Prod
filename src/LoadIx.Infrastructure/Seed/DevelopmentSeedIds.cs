using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Seed;

public static class DevelopmentSeedIds
{
    public static readonly TenantId TenantId =
        TenantId.From(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    public static readonly Guid ActorId =
        Guid.Parse("10000000-0000-0000-0000-000000000002");

    public static readonly Guid DriverUserId =
        Guid.Parse("10000000-0000-0000-0000-000000000003");

    public static readonly Guid DriverId =
        Guid.Parse("00000160-0000-0000-0000-000000000001");

    public static readonly Guid JohannesburgBranchId =
        Guid.Parse("11000000-0000-0000-0000-000000000001");

    public static readonly Guid MpumalangaBranchId =
        Guid.Parse("11000000-0000-0000-0000-000000000002");

    public static readonly Guid[] CustomerIds =
    [
        Guid.Parse("12000000-0000-0000-0000-000000000001"),
        Guid.Parse("12000000-0000-0000-0000-000000000002"),
        Guid.Parse("12000000-0000-0000-0000-000000000003"),
    ];

    public static readonly Guid[] LocationIds =
        Enumerable.Range(1, 6)
            .Select(number => Guid.Parse(
                $"00000140-0000-0000-0000-{number:000000000000}"))
            .ToArray();

    public static readonly Guid[] CommodityIds =
        Enumerable.Range(1, 4)
            .Select(number => Guid.Parse(
                $"00000150-0000-0000-0000-{number:000000000000}"))
            .ToArray();
}
