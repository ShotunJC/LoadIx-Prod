using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class RawWeightReading : Entity<Guid>, ITenantOwned
{
    private RawWeightReading(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid weighbridgeDeviceId,
        decimal displayedValue,
        WeightUnit unit,
        bool isStable,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc,
        string? vehicleRegistration,
        string serialMessageMetadata,
        string parserVersion,
        string correlationId,
        Guid edgeCommandId)
        : base(id)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        WeighbridgeDeviceId = weighbridgeDeviceId;
        DisplayedValue = displayedValue;
        Unit = unit;
        IsStable = isStable;
        CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
        VehicleRegistration = vehicleRegistration;
        SerialMessageMetadata = serialMessageMetadata;
        ParserVersion = parserVersion;
        CorrelationId = correlationId;
        EdgeCommandId = edgeCommandId;
    }

    private RawWeightReading()
    {
        SerialMessageMetadata = string.Empty;
        ParserVersion = string.Empty;
        CorrelationId = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public Guid WeighbridgeDeviceId { get; private set; }

    public decimal DisplayedValue { get; private set; }

    public WeightUnit Unit { get; private set; }

    public bool IsStable { get; private set; }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public string? VehicleRegistration { get; private set; }

    public string SerialMessageMetadata { get; private set; }

    public string ParserVersion { get; private set; }

    public string CorrelationId { get; private set; }

    public Guid EdgeCommandId { get; private set; }

    public static Result<RawWeightReading> Create(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid weighbridgeDeviceId,
        decimal displayedValue,
        WeightUnit unit,
        bool isStable,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc,
        string? vehicleRegistration,
        string serialMessageMetadata,
        string parserVersion,
        string correlationId,
        Guid edgeCommandId)
    {
        var error = DomainValidation.Required(
                serialMessageMetadata,
                "weight_reading.serial_message_metadata",
                4000)
            ?? DomainValidation.Required(
                parserVersion,
                "weight_reading.parser_version",
                80)
            ?? DomainValidation.Required(
                correlationId,
                "weight_reading.correlation_id",
                128);
        if (id == Guid.Empty
            || tenantId.IsEmpty
            || edgeDeviceId == Guid.Empty
            || weighbridgeDeviceId == Guid.Empty
            || edgeCommandId == Guid.Empty)
        {
            error = new DomainError(
                "weight_reading.identifiers_required",
                "Reading, tenant, device, weighbridge, and command identifiers are required.");
        }
        else if (displayedValue < 0m)
        {
            error = new DomainError(
                "weight_reading.value_negative",
                "A displayed weight value cannot be negative.");
        }
        else if (!Enum.IsDefined(unit))
        {
            error = new DomainError(
                "weight_reading.unit_invalid",
                "The weight unit is not supported.");
        }
        else if (decimal.Round(displayedValue, 3, MidpointRounding.ToEven)
            != displayedValue)
        {
            error = new DomainError(
                "weight_reading.precision_invalid",
                "A displayed weight supports at most three decimal places.");
        }
        else if (vehicleRegistration?.Trim().Length > 30)
        {
            error = new DomainError(
                "weight_reading.vehicle_registration_too_long",
                "Vehicle registration cannot exceed 30 characters.");
        }

        return error is not null
            ? Result<RawWeightReading>.Failure(error)
            : Result<RawWeightReading>.Success(new RawWeightReading(
                id,
                tenantId,
                edgeDeviceId,
                weighbridgeDeviceId,
                displayedValue,
                unit,
                isStable,
                capturedAtUtc,
                receivedAtUtc,
                DomainValidation.Optional(vehicleRegistration)?.ToUpperInvariant(),
                serialMessageMetadata.Trim(),
                parserVersion.Trim(),
                correlationId.Trim(),
                edgeCommandId));
    }
}
