using LoadIx.Domain.Common;

namespace LoadIx.Application.Reporting;

/// <summary>
/// Defines the tolerance calculation used by SQL-backed reporting projections.
/// Destination net weight is preferred, source net weight is the fallback, and
/// a trip without a complete net-weight pair is not an exception.
/// </summary>
public static class ReportingToleranceCalculator
{
    public static decimal? SelectMeasuredTonnes(
        decimal? sourceGross,
        decimal? sourceTare,
        decimal? destinationGross,
        decimal? destinationTare,
        WeightUnit unit)
    {
        var measured = destinationGross.HasValue && destinationTare.HasValue
            ? destinationGross.Value - destinationTare.Value
            : sourceGross.HasValue && sourceTare.HasValue
                ? sourceGross.Value - sourceTare.Value
                : (decimal?)null;
        return unit == WeightUnit.Kilograms ? measured / 1000m : measured;
    }

    public static bool IsOutsideTolerance(
        decimal plannedQuantity,
        WeightUnit unit,
        decimal? sourceGross,
        decimal? sourceTare,
        decimal? destinationGross,
        decimal? destinationTare,
        decimal tolerancePercent)
    {
        var plannedTonnes = unit == WeightUnit.Kilograms
            ? plannedQuantity / 1000m
            : plannedQuantity;
        var measuredTonnes = SelectMeasuredTonnes(
            sourceGross, sourceTare, destinationGross, destinationTare, unit);

        return plannedTonnes > 0m
            && measuredTonnes.HasValue
            && Math.Abs(measuredTonnes.Value - plannedTonnes)
                > plannedTonnes * tolerancePercent / 100m;
    }
}
