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

    public static readonly Guid[] CustomerIds = Enumerable.Range(1, 5)
        .Select(number => DeterministicGuid(120, number))
        .ToArray();

    public static readonly Guid[] LocationIds =
        Enumerable.Range(1, 8)
            .Select(number => DeterministicGuid(140, number))
            .ToArray();

    public static readonly Guid[] CommodityIds =
        Enumerable.Range(1, 5)
            .Select(number => DeterministicGuid(150, number))
            .ToArray();

    public static readonly Guid[] ContractIds = Enumerable.Range(1, 4)
        .Select(number => DeterministicGuid(190, number)).ToArray();

    public static readonly Guid[] RouteIds = Enumerable.Range(1, 4)
        .Select(number => DeterministicGuid(191, number)).ToArray();

    public static readonly Guid[] RateIds = Enumerable.Range(1, 4)
        .Select(number => DeterministicGuid(192, number)).ToArray();

    public static readonly Guid[] InstructionIds = Enumerable.Range(1, 8)
        .Select(number => DeterministicGuid(193, number)).ToArray();

    public static readonly Guid[] TripIds = Enumerable.Range(1, 32)
        .Select(number => DeterministicGuid(194, number)).ToArray();

    public static Guid DeterministicGuid(int group, int number) =>
        Guid.Parse($"{group:00000000}-0000-0000-0000-{number:000000000000}");
}
