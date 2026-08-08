using FluentAssertions;
using LoadIx.Domain.Commercial;
using LoadIx.Domain.Common;
using LoadIx.Domain.Finance;
using LoadIx.Domain.Trips;

namespace LoadIx.UnitTests.Domain;

public sealed class TripFinancialsTests
{
    private static readonly TenantId Tenant =
        TenantId.From(Guid.Parse("70000000-0000-0000-0000-000000000001"));
    private static readonly DateOnly Today = new(2026, 7, 30);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculatesRevenueTaxAndProfitabilityFromImmutableRateSnapshot()
    {
        var financials = CreateFinancials(
            ContractRateType.BasePlusPerTonne,
            billableTonnes: 12.345m,
            baseFee: 250m,
            perTonne: 100.1234m,
            fuel: 10m,
            taxRatePercent: 15m);

        financials.TaxExclusiveAmount.Should().Be(1634.63m);
        financials.TaxAmount.Should().Be(245.1945m);
        financials.Total.Should().Be(1879.8245m);
        financials.CalculationVersion.Should().Be("trip-financials-v1");

        var cost = CostEntry.Create(
            Guid.NewGuid(),
            Tenant,
            financials.Id,
            CostEntryType.FuelActual,
            400.5555m,
            "ZAR",
            "Approved diesel cost",
            Guid.NewGuid(),
            Now,
            isAdjustment: false);

        financials.AddCost(cost.Value).IsSuccess.Should().BeTrue();
        financials.TotalCost.Should().Be(400.5555m);
        financials.GrossMargin.Should().Be(1234.0745m);
        financials.RevenuePerTonne.Should().Be(132.4123m);
    }

    [Fact]
    public void RequiresReviewedTaxTreatmentAtApproval()
    {
        var result = TripFinancials.Create(
            Guid.NewGuid(),
            Tenant,
            Guid.NewGuid(),
            "TRIP-001",
            Guid.NewGuid(),
            "Synthetic customer",
            Guid.NewGuid(),
            "CTR-001",
            Snapshot(ContractRateType.FixedPerTrip, fixedAmount: 1000m),
            0m,
            TaxTreatment.NotReviewed,
            0m,
            Now);

        result.IsSuccess.Should().BeTrue();
        var approval = result.Value.Approve(Guid.NewGuid(), Now, "Reviewed");
        approval.IsFailure.Should().BeTrue();
        approval.Error!.Code.Should().Be("finance.tax_not_reviewed");
    }

    [Fact]
    public void LocksApprovedFinancialsButAllowsExplicitAdjustments()
    {
        var financials = CreateFinancials(
            ContractRateType.FixedPerTrip,
            billableTonnes: 0m,
            fixedAmount: 1000m);

        financials.Approve(Guid.NewGuid(), Now, "Reviewed").IsSuccess.Should()
            .BeTrue();

        var normalCost = CostEntry.Create(
            Guid.NewGuid(),
            Tenant,
            financials.Id,
            CostEntryType.Toll,
            75m,
            "ZAR",
            "Late toll slip",
            Guid.NewGuid(),
            Now,
            isAdjustment: false).Value;
        financials.AddCost(normalCost).IsFailure.Should().BeTrue();

        var adjustment = CostEntry.Create(
            Guid.NewGuid(),
            Tenant,
            financials.Id,
            CostEntryType.MiscellaneousApprovedCost,
            75m,
            "ZAR",
            "Approved post-invoice adjustment",
            Guid.NewGuid(),
            Now,
            isAdjustment: true).Value;
        financials.AddAdjustment(adjustment).IsSuccess.Should().BeTrue();
        financials.TotalCost.Should().Be(75m);
    }

    [Fact]
    public void PreventsDuplicateInvoiceLinesAndMarksFinancialsAsInvoiced()
    {
        var financials = CreateFinancials(
            ContractRateType.FixedPerTrip,
            billableTonnes: 0m,
            fixedAmount: 1000m);
        financials.Approve(Guid.NewGuid(), Now, "Ready to invoice");

        var batch = InvoiceBatch.Create(
            Guid.NewGuid(),
            Tenant,
            "INV-20260730-0001",
            financials.CustomerId,
            financials.CustomerName,
            financials.TransportContractId,
            financials.ContractNumber,
            financials.CurrencyCode,
            Now).Value;
        var line = InvoiceBatchLine.Create(Guid.NewGuid(), batch.Id, financials)
            .Value;

        batch.AddLine(line).IsSuccess.Should().BeTrue();
        batch.AddLine(line).IsFailure.Should().BeTrue();

        financials.AttachInvoiceBatch(batch.Id).IsSuccess.Should().BeTrue();
        financials.AttachInvoiceBatch(Guid.NewGuid()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ResolvesBillableTonnesFromTheRateSourceAndConvertsKilograms()
    {
        var trip = CreateTrip(
            ContractRateType.SourceNetTonne,
            WeightUnit.Kilograms,
            30_000m);
        trip.RecordSourceWeighbridge(42_000m, 12_000m, "SOURCE");
        trip.RecordDestinationWeighbridge(41_500m, 12_000m, "DESTINATION");

        trip.ResolveBillableTonnes().Value.Should().Be(30m);
    }

    private static TripFinancials CreateFinancials(
        ContractRateType rateType,
        decimal billableTonnes,
        decimal? fixedAmount = null,
        decimal? baseFee = null,
        decimal? perTonne = null,
        decimal? fuel = null,
        decimal taxRatePercent = 0m) =>
        TripFinancials.Create(
            Guid.NewGuid(),
            Tenant,
            Guid.NewGuid(),
            "TRIP-001",
            Guid.NewGuid(),
            "Synthetic customer",
            Guid.NewGuid(),
            "CTR-001",
            Snapshot(rateType, fixedAmount, baseFee, perTonne, fuel),
            billableTonnes,
            taxRatePercent > 0 ? TaxTreatment.StandardRated : TaxTreatment.ZeroRated,
            taxRatePercent,
            Now).Value;

    private static TripRateSnapshot Snapshot(
        ContractRateType rateType,
        decimal? fixedAmount = null,
        decimal? baseFee = null,
        decimal? perTonne = null,
        decimal? fuel = null)
    {
        var routeId = Guid.NewGuid();
        var contract = TransportContract.Create(
            Guid.NewGuid(),
            Tenant,
            Guid.NewGuid(),
            "CTR-001",
            "Finance test contract",
            Today.AddDays(-1),
            Today.AddDays(30),
            "ZAR",
            30,
            1m,
            null,
            [
                new ContractRouteDefinition(
                    routeId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "RT-001"),
            ],
            [Guid.NewGuid()],
            [
                new ContractRateDefinition(
                    Guid.NewGuid(),
                    routeId,
                    rateType,
                    Today.AddDays(-1),
                    Today.AddDays(30),
                    fixedAmount,
                    baseFee,
                    perTonne,
                    null,
                    fuel),
            ]).Value;

        var result = ContractRateCalculator.Calculate(
            contract.Rates.Single(),
            new RateCalculationInput(12.345m, 12.345m, 12.345m, Today),
            "ZAR");
        return TripRateSnapshot.Create(result.Value.RateSnapshot);
    }

    private static Trip CreateTrip(
        ContractRateType rateType,
        WeightUnit unit,
        decimal plannedQuantity) =>
        Trip.Create(
            Guid.NewGuid(),
            Tenant,
            new TripDefinition(
                "TRIP-BILLABLE",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "CTR-001",
                "version",
                Guid.NewGuid(),
                "ROUTE",
                Guid.NewGuid(),
                plannedQuantity,
                unit,
                Now,
                Now.AddHours(1)),
            Snapshot(rateType, perTonne: 100m)).Value;
}
