using FluentAssertions;
using LoadIx.Application.Reporting;
using LoadIx.Domain.Common;

namespace LoadIx.UnitTests.Reporting;

public sealed class ReportingToleranceCalculatorTests
{
    [Fact]
    public void MissingWeightsAreNotOutsideTolerance()
    {
        ReportingToleranceCalculator.IsOutsideTolerance(
            30m, WeightUnit.Tonnes, null, null, null, null, 5m)
            .Should().BeFalse();
    }

    [Fact]
    public void SourceWeightIsUsedWhenDestinationWeightIsMissing()
    {
        ReportingToleranceCalculator.IsOutsideTolerance(
            30m, WeightUnit.Tonnes, 31.6m, 0m, null, null, 5m)
            .Should().BeTrue();
    }

    [Fact]
    public void DestinationWeightIsPreferredWhenBothMeasurementsExist()
    {
        ReportingToleranceCalculator.SelectMeasuredTonnes(
            40m, 0m, 31m, 0m, WeightUnit.Tonnes)
            .Should().Be(31m);
    }

    [Fact]
    public void ExactToleranceBoundaryIsIncludedButJustOutsideIsNot()
    {
        ReportingToleranceCalculator.IsOutsideTolerance(
            30m, WeightUnit.Tonnes, 31.5m, 0m, null, null, 5m)
            .Should().BeFalse();
        ReportingToleranceCalculator.IsOutsideTolerance(
            30m, WeightUnit.Tonnes, 31.51m, 0m, null, null, 5m)
            .Should().BeTrue();
    }

    [Fact]
    public void KilogramsAreConvertedToTonnesBeforeComparison()
    {
        ReportingToleranceCalculator.SelectMeasuredTonnes(
            31_500m, 0m, null, null, WeightUnit.Kilograms)
            .Should().Be(31.5m);
        ReportingToleranceCalculator.IsOutsideTolerance(
            30_000m, WeightUnit.Kilograms, 31_500m, 0m, null, null, 5m)
            .Should().BeFalse();
        ReportingToleranceCalculator.IsOutsideTolerance(
            30_000m, WeightUnit.Kilograms, 31_501m, 0m, null, null, 5m)
            .Should().BeTrue();
    }
}
