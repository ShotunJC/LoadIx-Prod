using FluentAssertions;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;

namespace LoadIx.UnitTests.Domain;

public sealed class ContractRateCalculationTests
{
    private static readonly TenantId Tenant =
        TenantId.From(Guid.Parse("70000000-0000-0000-0000-000000000001"));
    private static readonly DateOnly Today = new(2026, 7, 28);

    public static TheoryData<
        ContractRateType,
        decimal?,
        decimal?,
        decimal?,
        decimal?,
        decimal?,
        decimal?,
        decimal> RateCases =>
        new()
        {
            { ContractRateType.FixedPerTrip, 1250m, null, null, null, null, null, 1250m },
            { ContractRateType.SourceNetTonne, null, null, 100m, 12.5m, null, null, 1250m },
            { ContractRateType.DestinationNetTonne, null, null, 80m, null, 12.5m, null, 1000m },
            { ContractRateType.ReconciledTonne, null, null, 75m, null, null, 12.5m, 937.5m },
            { ContractRateType.BasePlusPerTonne, null, 250m, 40m, null, null, 12.5m, 750m },
        };

    [Theory]
    [MemberData(nameof(RateCases))]
    public void CalculatesEveryRateType(
        ContractRateType type,
        decimal? fixedAmount,
        decimal? baseFee,
        decimal? perTonne,
        decimal? source,
        decimal? destination,
        decimal? reconciled,
        decimal expected)
    {
        var rate = CreateRate(
            type,
            fixedAmount,
            baseFee,
            type == ContractRateType.FixedPerTrip ? null : perTonne);

        var result = ContractRateCalculator.Calculate(
            rate,
            new RateCalculationInput(source, destination, reconciled, Today),
            "ZAR");

        result.IsSuccess.Should().BeTrue();
        result.Value.Total.Amount.Should().Be(expected);
    }

    [Fact]
    public void AppliesFuelBeforeMinimumAndUsesExplicitRounding()
    {
        var rate = CreateRate(
            ContractRateType.ReconciledTonne,
            perTonne: 33.3333m,
            minimum: 1100m,
            fuel: 7.5m,
            decimals: 2);

        var result = ContractRateCalculator.Calculate(
            rate,
            new RateCalculationInput(null, null, 30.001m, Today),
            "ZAR");

        result.Value.Total.Amount.Should().Be(1100m);
        result.Value.Breakdown.Select(line => line.Code)
            .Should().Equal("tonnage", "fuel", "minimum");
        result.Value.RateSnapshot.RoundingMode
            .Should().Be(MidpointRounding.AwayFromZero);
    }

    [Fact]
    public void RoundsWeightToThreeDecimalsAndMoneyAwayFromZero()
    {
        var rate = CreateRate(
            ContractRateType.ReconciledTonne,
            perTonne: 1.005m,
            decimals: 2);

        var result = ContractRateCalculator.Calculate(
            rate,
            new RateCalculationInput(null, null, 1m, Today),
            "ZAR");

        result.Value.BillableTonnes.Should().Be(1m);
        result.Value.Total.Amount.Should().Be(1.01m);
    }

    [Fact]
    public void RejectsRateOutsideItsEffectiveDates()
    {
        var rate = CreateRate(
            ContractRateType.FixedPerTrip,
            fixedAmount: 100m,
            effectiveFrom: Today.AddDays(1));

        var result = ContractRateCalculator.Calculate(
            rate,
            new RateCalculationInput(null, null, null, Today),
            "ZAR");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("rate.not_effective");
    }

    [Fact]
    public void CalculationSnapshotDoesNotChangeWithLaterRateDefinitions()
    {
        var original = CreateRate(
            ContractRateType.FixedPerTrip,
            fixedAmount: 100m);
        var calculated = ContractRateCalculator.Calculate(
            original,
            new RateCalculationInput(null, null, null, Today),
            "ZAR").Value;

        _ = CreateRate(ContractRateType.FixedPerTrip, fixedAmount: 250m);

        calculated.Total.Amount.Should().Be(100m);
        calculated.RateSnapshot.FixedAmount.Should().Be(100m);
        calculated.InputSnapshot.CalculationDate.Should().Be(Today);
    }

    [Fact]
    public void RejectsOverlappingEffectiveRates()
    {
        var routeId = Guid.NewGuid();
        var contract = CreateContract(
            [
                RateDefinition(routeId, Today, Today.AddDays(10)),
                RateDefinition(routeId, Today.AddDays(10), Today.AddDays(20)),
            ],
            routeId);

        contract.IsFailure.Should().BeTrue();
        contract.Error!.Code.Should().Be("contract.rate_dates_overlap");
    }

    [Fact]
    public void IncompleteContractCannotBeActivated()
    {
        var contract = TransportContract.Create(
            Guid.NewGuid(),
            Tenant,
            Guid.NewGuid(),
            "CTR-EMPTY",
            "Empty contract",
            Today.AddDays(-1),
            Today.AddDays(30),
            "ZAR",
            30,
            1m,
            null,
            [],
            [],
            []);

        contract.Value.Activate().IsFailure.Should().BeTrue();
    }

    private static ContractRate CreateRate(
        ContractRateType type,
        decimal? fixedAmount = null,
        decimal? baseFee = null,
        decimal? perTonne = null,
        decimal? minimum = null,
        decimal? fuel = null,
        int decimals = 2,
        DateOnly? effectiveFrom = null)
    {
        var routeId = Guid.NewGuid();
        return CreateContract(
            [
                new ContractRateDefinition(
                    Guid.NewGuid(),
                    routeId,
                    type,
                    effectiveFrom ?? Today.AddDays(-30),
                    Today.AddDays(30),
                    fixedAmount,
                    baseFee,
                    perTonne,
                    minimum,
                    fuel,
                    decimals),
            ],
            routeId).Value.Rates.Single();
    }

    private static Result<TransportContract> CreateContract(
        IReadOnlyCollection<ContractRateDefinition> rates,
        Guid routeId) =>
        TransportContract.Create(
            Guid.NewGuid(),
            Tenant,
            Guid.NewGuid(),
            "CTR-TEST",
            "Calculation test",
            Today.AddDays(-60),
            Today.AddDays(60),
            "ZAR",
            30,
            1m,
            null,
            [
                new ContractRouteDefinition(
                    routeId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "RT-01"),
            ],
            [Guid.NewGuid()],
            rates);

    private static ContractRateDefinition RateDefinition(
        Guid routeId,
        DateOnly from,
        DateOnly to) =>
        new(
            Guid.NewGuid(),
            routeId,
            ContractRateType.FixedPerTrip,
            from,
            to,
            100m,
            null,
            null,
            null,
            null);
}
