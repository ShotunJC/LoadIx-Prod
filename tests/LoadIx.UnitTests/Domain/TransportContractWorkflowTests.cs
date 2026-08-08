using FluentAssertions;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;

namespace LoadIx.UnitTests.Domain;

public sealed class TransportContractWorkflowTests
{
    private static readonly TenantId Tenant = TenantId.New();

    [Fact]
    public void ActivationRejectsRateWhoseRequiredWeightSourceIsDisabled()
    {
        var routeId = Guid.NewGuid();
        var contract = Create(
            routeId,
            ContractRateType.SourceNetTonne,
            sourceRequired: false,
            destinationRequired: true);

        contract.Activate().Error!.Code.Should().Be(
            "contract.activation_quantity_source_missing");
    }

    [Fact]
    public void ActivationSupportsBothOneOrNeitherWeighbridgeWithCompatibleRates()
    {
        Create(Guid.NewGuid(), ContractRateType.ReconciledTonne, true, true)
            .Activate().IsSuccess.Should().BeTrue();
        Create(Guid.NewGuid(), ContractRateType.ReconciledTonne, true, false)
            .Activate().IsSuccess.Should().BeTrue();
        Create(Guid.NewGuid(), ContractRateType.FixedPerTrip, false, false)
            .Activate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UpdatePreservesSubmittedRouteAndRateIdentities()
    {
        var routeId = Guid.NewGuid();
        var rateId = Guid.NewGuid();
        var contract = Create(
            routeId,
            ContractRateType.FixedPerTrip,
            true,
            true,
            rateId);

        var result = contract.Update(
            contract.CustomerId,
            contract.ContractNumber,
            "Updated name",
            contract.StartDate,
            contract.EndDate,
            contract.DefaultCurrency,
            contract.PaymentTermsDays,
            contract.WeightTolerancePercent,
            "Future trips only",
            [Route(routeId, true, false)],
            [contract.Commodities.Single().CommodityId],
            [Rate(rateId, routeId, ContractRateType.FixedPerTrip)]);

        result.IsSuccess.Should().BeTrue();
        contract.Routes.Single().Id.Should().Be(routeId);
        contract.Rates.Single().Id.Should().Be(rateId);
        contract.Routes.Single().DestinationWeightCaptureRequired.Should().BeFalse();
    }

    private static TransportContract Create(
        Guid routeId,
        ContractRateType type,
        bool sourceRequired,
        bool destinationRequired,
        Guid? rateId = null) =>
        TransportContract.Create(
            Guid.NewGuid(),
            Tenant,
            Guid.NewGuid(),
            $"CON-{Guid.NewGuid():N}",
            "Test contract",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            "ZAR",
            30,
            1.5m,
            null,
            [Route(routeId, sourceRequired, destinationRequired)],
            [Guid.NewGuid()],
            [Rate(rateId ?? Guid.NewGuid(), routeId, type)]).Value;

    private static ContractRouteDefinition Route(
        Guid id,
        bool sourceRequired,
        bool destinationRequired) =>
        new(
            id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SOURCE-DESTINATION",
            sourceRequired,
            destinationRequired);

    private static ContractRateDefinition Rate(
        Guid id,
        Guid routeId,
        ContractRateType type) =>
        new(
            id,
            routeId,
            type,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            type == ContractRateType.FixedPerTrip ? 1000m : null,
            type == ContractRateType.BasePlusPerTonne ? 100m : null,
            type == ContractRateType.FixedPerTrip ? null : 50m,
            null,
            null);
}
