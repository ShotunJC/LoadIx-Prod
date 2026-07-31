using LoadIx.Domain.Commercial;

namespace LoadIx.Domain.Trips;

public sealed class TripRateSnapshot
{
    private TripRateSnapshot()
    {
        CurrencyCode = string.Empty;
    }

    public Guid RateId { get; private set; }

    public ContractRateType RateType { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public decimal? FixedAmount { get; private set; }

    public decimal? BaseFee { get; private set; }

    public decimal? PerTonneAmount { get; private set; }

    public decimal? MinimumCharge { get; private set; }

    public decimal? FuelSurchargePercent { get; private set; }

    public int RoundingDecimalPlaces { get; private set; }

    public MidpointRounding RoundingMode { get; private set; }

    public string CurrencyCode { get; private set; }

    public static TripRateSnapshot Create(
        ContractRateSnapshot snapshot) =>
        new()
        {
            RateId = snapshot.RateId,
            RateType = snapshot.RateType,
            EffectiveFrom = snapshot.EffectiveFrom,
            EffectiveTo = snapshot.EffectiveTo,
            FixedAmount = snapshot.FixedAmount,
            BaseFee = snapshot.BaseFee,
            PerTonneAmount = snapshot.PerTonneAmount,
            MinimumCharge = snapshot.MinimumCharge,
            FuelSurchargePercent = snapshot.FuelSurchargePercent,
            RoundingDecimalPlaces = snapshot.RoundingDecimalPlaces,
            RoundingMode = snapshot.RoundingMode,
            CurrencyCode = snapshot.CurrencyCode,
        };
}
