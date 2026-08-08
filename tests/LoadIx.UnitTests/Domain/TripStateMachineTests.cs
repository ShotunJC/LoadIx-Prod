using FluentAssertions;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Trips;

namespace LoadIx.UnitTests.Domain;

public sealed class TripStateMachineTests
{
    private static readonly TenantId Tenant =
        TenantId.From(Guid.Parse("72000000-0000-0000-0000-000000000001"));
    private static readonly Guid Actor =
        Guid.Parse("72000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExecutesTheExplicitLifecycleAndRecordsEveryTransition()
    {
        var trip = Create();

        trip.Assign(
            Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], Context())
            .IsSuccess.Should().BeTrue();
        trip.Dispatch(Context()).IsSuccess.Should().BeTrue();
        trip.ConfirmCollectionArrival(Context()).IsSuccess.Should().BeTrue();
        trip.StartLoading(Context()).IsSuccess.Should().BeTrue();
        trip.RecordSourceWeighbridge(42m, 12m, "SOURCE-001")
            .IsSuccess.Should().BeTrue();
        trip.CompleteCollection(Context()).IsSuccess.Should().BeTrue();
        trip.DepartCollection(Context()).IsSuccess.Should().BeTrue();
        trip.ConfirmDeliveryArrival(Context()).IsSuccess.Should().BeTrue();
        trip.StartUnloading(Context()).IsSuccess.Should().BeTrue();
        trip.RecordDestinationWeighbridge(41.8m, 12m, "DEST-001")
            .IsSuccess.Should().BeTrue();
        trip.CompleteDelivery(Context()).IsSuccess.Should().BeTrue();
        trip.SubmitForReconciliation(Context()).IsSuccess.Should().BeTrue();
        trip.ConfirmReconciliation().IsSuccess.Should().BeTrue();
        trip.ConfirmRequiredDocuments();
        trip.Complete(Context()).IsSuccess.Should().BeTrue();

        trip.Status.Should().Be(TripStatus.Completed);
        trip.SourceNetWeight.Should().Be(30m);
        trip.DestinationNetWeight.Should().Be(29.8m);
        trip.Transitions.Should().HaveCount(11);
        trip.Transitions.Should().OnlyContain(item => item.ActorUserId == Actor);
        trip.DomainEvents.Should().HaveCount(11);
    }

    [Fact]
    public void RejectsInvalidTransitionsEvenWhenCalledDirectly()
    {
        var trip = Create();

        trip.Dispatch(Context()).IsFailure.Should().BeTrue();
        trip.ConfirmCollectionArrival(Context()).IsFailure.Should().BeTrue();
        trip.Complete(Context()).IsFailure.Should().BeTrue();
        trip.Status.Should().Be(TripStatus.Planned);
        trip.Transitions.Should().BeEmpty();
    }

    [Fact]
    public void AssignmentAcceptsTwoTrailersAndRejectsAThird()
    {
        var trip = Create();

        trip.Assign(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [Guid.NewGuid(), Guid.NewGuid()],
            Context()).IsSuccess.Should().BeTrue();

        var anotherTrip = Create();
        var result = anotherTrip.Assign(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            Context());

        result.Error!.Code.Should().Be("trip.assignment_invalid");
        anotherTrip.Status.Should().Be(TripStatus.Planned);
    }

    [Fact]
    public void RequiresWeighbridgeReconciliationAndDocuments()
    {
        var trip = Create();
        AssignAndAdvanceToLoading(trip);

        trip.CompleteCollection(Context()).Error!.Code
            .Should().Be("trip.required_evidence_missing");
        trip.RecordSourceWeighbridge(30m, 30m, "INVALID")
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CancellationAndRejectionRequireReasonsAndAreTerminal()
    {
        var trip = Create();

        trip.Cancel(Context()).IsFailure.Should().BeTrue();
        trip.Cancel(Context("Synthetic customer cancellation"))
            .IsSuccess.Should().BeTrue();
        trip.Assign(Guid.NewGuid(), Guid.NewGuid(), [], Context())
            .IsFailure.Should().BeTrue();
        trip.Status.Should().Be(TripStatus.Cancelled);
        trip.CancellationOrRejectionReason.Should()
            .Be("Synthetic customer cancellation");
    }

    [Fact]
    public void PostDispatchReassignmentAndStatusOverrideRequireManualAuthority()
    {
        var trip = Create();
        trip.Assign(Guid.NewGuid(), Guid.NewGuid(), [], Context());
        trip.Dispatch(Context());

        trip.Reassign(Guid.NewGuid(), Guid.NewGuid(), [], Context())
            .IsFailure.Should().BeTrue();
        trip.Reassign(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            Context("Vehicle breakdown", true)).IsSuccess.Should().BeTrue();
        trip.ManualOverride(
            TripStatus.AtDelivery,
            Context("Controller confirmed exceptional movement", true))
            .IsSuccess.Should().BeTrue();

        trip.Status.Should().Be(TripStatus.AtDelivery);
        trip.Transitions.Last().IsManualOverride.Should().BeTrue();
        trip.Assignments.Should().HaveCount(2);
        trip.Assignments.Count(item => item.EndedAtUtc is null).Should().Be(1);
    }

    [Fact]
    public void FinalisedTripsRejectWeighbridgeMutation()
    {
        var trip = Create();
        AssignAndAdvanceToLoading(trip);
        trip.RecordSourceWeighbridge(42m, 12m, "SOURCE-001");
        trip.CompleteCollection(Context());
        trip.DepartCollection(Context());
        trip.ConfirmDeliveryArrival(Context());
        trip.StartUnloading(Context());
        trip.RecordDestinationWeighbridge(41.8m, 12m, "DEST-001");
        trip.CompleteDelivery(Context());
        trip.SubmitForReconciliation(Context());
        trip.ConfirmReconciliation();
        trip.ConfirmRequiredDocuments();
        trip.Complete(Context());

        trip.RecordSourceWeighbridge(43m, 12m, "ALTERED")
            .Error!.Code.Should().Be("trip.weighbridge_terminal");
        trip.RecordDestinationWeighbridge(43m, 12m, "ALTERED")
            .Error!.Code.Should().Be("trip.weighbridge_terminal");
        trip.SourceGrossWeight.Should().Be(42m);
        trip.DestinationGrossWeight.Should().Be(41.8m);
    }

    [Fact]
    public void CompletesWithoutWeightsWhenNeitherSiteRequiresCapture()
    {
        var trip = Create(sourceRequired: false, destinationRequired: false);
        AssignAndAdvanceToLoading(trip);

        trip.CompleteCollection(Context()).IsSuccess.Should().BeTrue();
        trip.DepartCollection(Context()).IsSuccess.Should().BeTrue();
        trip.ConfirmDeliveryArrival(Context()).IsSuccess.Should().BeTrue();
        trip.StartUnloading(Context()).IsSuccess.Should().BeTrue();
        trip.CompleteDelivery(Context()).IsSuccess.Should().BeTrue();
        trip.SubmitForReconciliation(Context()).IsSuccess.Should().BeTrue();
        trip.ReconciliationStatus.Should().Be(TripReconciliationStatus.NotApplicable);
        trip.QuantitySource.Should().Be(TripQuantitySource.NonWeighed);
        trip.IsReconciled.Should().BeTrue();
        trip.ConfirmRequiredDocuments();

        trip.Complete(Context()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ReconcilesFromTheOnlyConfiguredMeasurement()
    {
        var trip = Create(sourceRequired: true, destinationRequired: false);
        AssignAndAdvanceToLoading(trip);
        trip.RecordSourceWeighbridge(42m, 12m, "SOURCE-ONLY");
        trip.CompleteCollection(Context());
        trip.DepartCollection(Context());
        trip.ConfirmDeliveryArrival(Context());
        trip.StartUnloading(Context());

        trip.CompleteDelivery(Context()).IsSuccess.Should().BeTrue();
        trip.SubmitForReconciliation(Context()).IsSuccess.Should().BeTrue();

        trip.ReconciledQuantity.Should().Be(30m);
        trip.ReconciliationStatus.Should().Be(
            TripReconciliationStatus.SingleMeasurement);
        trip.QuantitySource.Should().Be(TripQuantitySource.SourceMeasured);
    }

    [Fact]
    public void ManualQuantityRequiresContractOptInAndOverrideAuthority()
    {
        var trip = Create(
            sourceRequired: false,
            destinationRequired: false,
            manualPermitted: true,
            rateType: ContractRateType.ManualQuantityPerTonne);

        trip.RecordManualQuantity(30m, Context("Customer docket"))
            .Error!.Code.Should().Be("trip.manual_quantity_permission_required");
        trip.RecordManualQuantity(
            30m,
            Context("Customer docket", true)).IsSuccess.Should().BeTrue();
        trip.ResolveBillableTonnes().Value.Should().Be(30m);
    }

    private static void AssignAndAdvanceToLoading(Trip trip)
    {
        trip.Assign(Guid.NewGuid(), Guid.NewGuid(), [], Context());
        trip.Dispatch(Context());
        trip.ConfirmCollectionArrival(Context());
        trip.StartLoading(Context());
    }

    private static Trip Create(
        bool sourceRequired = true,
        bool destinationRequired = true,
        bool manualPermitted = false,
        ContractRateType rateType = ContractRateType.FixedPerTrip)
    {
        var snapshot = TripRateSnapshot.Create(new ContractRateSnapshot(
            Guid.NewGuid(),
            rateType,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            rateType == ContractRateType.FixedPerTrip ? 1000m : null,
            null,
            rateType == ContractRateType.FixedPerTrip ? null : 100m,
            null,
            null,
            2,
            MidpointRounding.AwayFromZero,
            "ZAR"));
        return Trip.Create(
            Guid.NewGuid(),
            Tenant,
            new TripDefinition(
                "TRIP-TEST-001",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "CONTRACT-001",
                "AQID",
                Guid.NewGuid(),
                "MINE-TO-DEPOT",
                Guid.NewGuid(),
                30m,
                WeightUnit.Tonnes,
                Now.AddHours(1),
                Now.AddHours(8),
                sourceRequired,
                destinationRequired,
                manualPermitted,
                1.5m),
            snapshot).Value;
    }

    private static TripCommandContext Context(
        string? reason = null,
        bool isManualOverride = false) =>
        new(Actor, Now, reason, isManualOverride);
}
