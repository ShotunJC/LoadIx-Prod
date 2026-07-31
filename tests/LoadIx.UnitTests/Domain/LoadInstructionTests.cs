using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Instructions;

namespace LoadIx.UnitTests.Domain;

public sealed class LoadInstructionTests
{
    private static readonly TenantId Tenant =
        TenantId.From(Guid.Parse("71000000-0000-0000-0000-000000000001"));

    [Fact]
    public void RejectsNonPositivePlannedQuantity()
    {
        var result = Create(quantity: 0m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("instruction.quantity_invalid");
    }

    [Fact]
    public void EnforcesLifecycleAndCancelledTripRule()
    {
        var instruction = Create().Value;

        instruction.Plan().IsSuccess.Should().BeTrue();
        instruction.Plan().IsFailure.Should().BeTrue();
        instruction.Cancel("Customer withdrew the synthetic request")
            .IsSuccess.Should().BeTrue();
        instruction.GenerateTrips(1, [Guid.NewGuid()]).IsFailure.Should().BeTrue();
        instruction.Status.Should().Be(LoadInstructionStatus.Cancelled);
    }

    [Fact]
    public void CalculatesRemainingUnderAndOverDeliveryFromCompletedTrips()
    {
        var instruction = Create(quantity: 30m).Value;
        instruction.Plan();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        instruction.GenerateTrips(2, [first, second]).IsSuccess.Should().BeTrue();

        instruction.CompleteTrip(first, 16m).IsSuccess.Should().BeTrue();
        instruction.CompletedQuantity.Should().Be(16m);
        instruction.RemainingQuantity.Should().Be(14m);
        instruction.UnderDeliveredQuantity.Should().Be(14m);
        instruction.CompleteTrip(second, 16m).IsSuccess.Should().BeTrue();
        instruction.CompletedQuantity.Should().Be(32m);
        instruction.RemainingQuantity.Should().Be(-2m);
        instruction.OverDeliveredQuantity.Should().Be(2m);
        instruction.UnderDeliveredQuantity.Should().Be(0m);
        instruction.Status.Should().Be(LoadInstructionStatus.Completed);
    }

    [Fact]
    public void LocksOperationalFieldsAfterTripGeneration()
    {
        var instruction = Create().Value;
        instruction.Plan();
        instruction.GenerateTrips(1, [Guid.NewGuid()]);
        var changed = Definition() with { RequestedQuantity = 40m };

        var result = instruction.Update(changed);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("instruction.trip_locked_fields");
    }

    private static Result<LoadInstruction> Create(decimal quantity = 30m) =>
        LoadInstruction.Create(
            Guid.NewGuid(),
            Tenant,
            Definition() with { RequestedQuantity = quantity });

    private static LoadInstructionDefinition Definition()
    {
        var collection = new DateTimeOffset(2026, 8, 1, 6, 0, 0, TimeSpan.Zero);
        return new LoadInstructionDefinition(
            "LI-TEST-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            30m,
            WeightUnit.Tonnes,
            2,
            collection,
            collection.AddHours(2),
            collection.AddHours(4),
            collection.AddHours(8),
            "PO-SYNTHETIC",
            "CUSTOMER-SYNTHETIC",
            LoadInstructionPriority.Normal,
            "Synthetic unit-test instruction");
    }
}
