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

    private static void AssignAndAdvanceToLoading(Trip trip)
    {
        trip.Assign(Guid.NewGuid(), Guid.NewGuid(), [], Context());
        trip.Dispatch(Context());
        trip.ConfirmCollectionArrival(Context());
        trip.StartLoading(Context());
    }

    private static Trip Create()
    {
        var snapshot = TripRateSnapshot.Create(new ContractRateSnapshot(
            Guid.NewGuid(),
            ContractRateType.FixedPerTrip,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            1000m,
            null,
            null,
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
                Now.AddHours(8)),
            snapshot).Value;
    }

    private static TripCommandContext Context(
        string? reason = null,
        bool isManualOverride = false) =>
        new(Actor, Now, reason, isManualOverride);
}
