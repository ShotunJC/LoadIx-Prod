using LoadIx.Domain.Common;

namespace LoadIx.Domain.Locations;

public sealed class Location : AggregateRoot<Guid>, ITenantOwned
{
    private Location(
        Guid id,
        TenantId tenantId,
        Guid? customerId,
        string name,
        string code,
        LocationType type,
        Address physicalAddress,
        decimal? latitude,
        decimal? longitude,
        decimal? geofenceRadiusMetres,
        string? operatingInstructions,
        string? contactName,
        string? contactPhoneNumber,
        string? operatingHours)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Name = name;
        Code = code;
        Type = type;
        PhysicalAddress = physicalAddress;
        Latitude = latitude;
        Longitude = longitude;
        GeofenceRadiusMetres = geofenceRadiusMetres;
        OperatingInstructions = operatingInstructions;
        ContactName = contactName;
        ContactPhoneNumber = contactPhoneNumber;
        OperatingHours = operatingHours;
        IsActive = true;
    }

    private Location()
    {
        Name = string.Empty;
        Code = string.Empty;
        PhysicalAddress = null!;
    }

    public TenantId TenantId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public LocationType Type { get; private set; }

    public Address PhysicalAddress { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public decimal? GeofenceRadiusMetres { get; private set; }

    public string? OperatingInstructions { get; private set; }

    public string? ContactName { get; private set; }

    public string? ContactPhoneNumber { get; private set; }

    public string? OperatingHours { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Location> Create(
        Guid id,
        TenantId tenantId,
        Guid? customerId,
        string name,
        string code,
        LocationType type,
        Address physicalAddress,
        decimal? latitude,
        decimal? longitude,
        decimal? geofenceRadiusMetres,
        string? operatingInstructions,
        string? contactName = null,
        string? contactPhoneNumber = null,
        string? operatingHours = null)
    {
        var error = DomainValidation.Required(name, "location.name", 200)
            ?? DomainValidation.Required(code, "location.code", 40);

        if (id == Guid.Empty || tenantId.IsEmpty)
        {
            error = new DomainError(
                "location.identifiers_required",
                "Location and tenant identifiers are required.");
        }
        else if (!Enum.IsDefined(type))
        {
            error = new DomainError(
                "location.invalid_type",
                "Location type is not supported.");
        }
        else if (latitude is < -90m or > 90m)
        {
            error = new DomainError(
                "location.invalid_latitude",
                "Latitude must be between -90 and 90.");
        }
        else if (longitude is < -180m or > 180m)
        {
            error = new DomainError(
                "location.invalid_longitude",
                "Longitude must be between -180 and 180.");
        }
        else if (latitude.HasValue != longitude.HasValue)
        {
            error = new DomainError(
                "location.incomplete_coordinates",
                "Latitude and longitude must be supplied together.");
        }
        else if (geofenceRadiusMetres is <= 0m or > 100_000m)
        {
            error = new DomainError(
                "location.invalid_geofence",
                "Geofence radius must be greater than zero and no more than 100 km.");
        }
        else if (geofenceRadiusMetres.HasValue && !latitude.HasValue)
        {
            error = new DomainError(
                "location.geofence_coordinates_required",
                "A geofence requires coordinates.");
        }

        return error is not null
            ? Result<Location>.Failure(error)
            : Result<Location>.Success(new Location(
                id,
                tenantId,
                customerId,
                name.Trim(),
                code.Trim().ToUpperInvariant(),
                type,
                physicalAddress,
                latitude,
                longitude,
                geofenceRadiusMetres,
                DomainValidation.Optional(operatingInstructions),
                DomainValidation.Optional(contactName),
                DomainValidation.Optional(contactPhoneNumber),
                DomainValidation.Optional(operatingHours)));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public Result Update(
        Guid? customerId,
        string name,
        string code,
        LocationType type,
        Address physicalAddress,
        decimal? latitude,
        decimal? longitude,
        decimal? geofenceRadiusMetres,
        string? operatingInstructions,
        string? contactName,
        string? contactPhoneNumber,
        string? operatingHours)
    {
        var candidate = Create(
            Id,
            TenantId,
            customerId,
            name,
            code,
            type,
            physicalAddress,
            latitude,
            longitude,
            geofenceRadiusMetres,
            operatingInstructions,
            contactName,
            contactPhoneNumber,
            operatingHours);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error!);
        }

        CustomerId = candidate.Value.CustomerId;
        Name = candidate.Value.Name;
        Code = candidate.Value.Code;
        Type = candidate.Value.Type;
        PhysicalAddress = candidate.Value.PhysicalAddress;
        Latitude = candidate.Value.Latitude;
        Longitude = candidate.Value.Longitude;
        GeofenceRadiusMetres = candidate.Value.GeofenceRadiusMetres;
        OperatingInstructions = candidate.Value.OperatingInstructions;
        ContactName = candidate.Value.ContactName;
        ContactPhoneNumber = candidate.Value.ContactPhoneNumber;
        OperatingHours = candidate.Value.OperatingHours;
        return Result.Success();
    }
}
