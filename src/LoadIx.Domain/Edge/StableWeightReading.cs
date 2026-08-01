using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class StableWeightReading : Entity<Guid>, ITenantOwned
{
    private StableWeightReading(
        Guid id,
        TenantId tenantId,
        Guid rawWeightReadingId,
        Guid edgeDeviceId,
        Guid weighbridgeDeviceId,
        decimal displayedValue,
        WeightUnit unit,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc,
        string? vehicleRegistration,
        string correlationId)
        : base(id)
    {
        TenantId = tenantId;
        RawWeightReadingId = rawWeightReadingId;
        EdgeDeviceId = edgeDeviceId;
        WeighbridgeDeviceId = weighbridgeDeviceId;
        DisplayedValue = displayedValue;
        Unit = unit;
        CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
        VehicleRegistration = vehicleRegistration;
        CorrelationId = correlationId;
    }

    private StableWeightReading()
    {
        CorrelationId = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid RawWeightReadingId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public Guid WeighbridgeDeviceId { get; private set; }

    public decimal DisplayedValue { get; private set; }

    public WeightUnit Unit { get; private set; }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public string? VehicleRegistration { get; private set; }

    public string CorrelationId { get; private set; }

    public static Result<StableWeightReading> Create(
        Guid id,
        TenantId tenantId,
        Guid rawWeightReadingId,
        Guid edgeDeviceId,
        Guid weighbridgeDeviceId,
        decimal displayedValue,
        WeightUnit unit,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc,
        string? vehicleRegistration,
        string correlationId)
    {
        var error = DomainValidation.Required(
            correlationId,
            "stable_weight_reading.correlation_id",
            128);
        if (id == Guid.Empty
            || tenantId.IsEmpty
            || rawWeightReadingId == Guid.Empty
            || edgeDeviceId == Guid.Empty
            || weighbridgeDeviceId == Guid.Empty)
        {
            error = new DomainError(
                "stable_weight_reading.identifiers_required",
                "Stable reading, tenant, raw reading, device, and weighbridge identifiers are required.");
        }
        else if (displayedValue < 0m)
        {
            error = new DomainError(
                "stable_weight_reading.value_negative",
                "A stable displayed weight value cannot be negative.");
        }
        else if (!Enum.IsDefined(unit))
        {
            error = new DomainError(
                "stable_weight_reading.unit_invalid",
                "The weight unit is not supported.");
        }
        else if (decimal.Round(displayedValue, 3, MidpointRounding.ToEven)
            != displayedValue)
        {
            error = new DomainError(
                "stable_weight_reading.precision_invalid",
                "A stable displayed weight supports at most three decimal places.");
        }
        else if (vehicleRegistration?.Trim().Length > 30)
        {
            error = new DomainError(
                "stable_weight_reading.vehicle_registration_too_long",
                "Vehicle registration cannot exceed 30 characters.");
        }

        return error is not null
            ? Result<StableWeightReading>.Failure(error)
            : Result<StableWeightReading>.Success(new StableWeightReading(
                id,
                tenantId,
                rawWeightReadingId,
                edgeDeviceId,
                weighbridgeDeviceId,
                displayedValue,
                unit,
                capturedAtUtc,
                receivedAtUtc,
                DomainValidation.Optional(vehicleRegistration)?.ToUpperInvariant(),
                correlationId.Trim()));
    }
}
