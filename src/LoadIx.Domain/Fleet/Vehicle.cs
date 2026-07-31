using LoadIx.Domain.Common;

namespace LoadIx.Domain.Fleet;

public sealed class Vehicle : AggregateRoot<Guid>, ITenantOwned
{
    private Vehicle(
        Guid id,
        TenantId tenantId,
        Guid branchId,
        string registration,
        string fleetNumber,
        string make,
        string model,
        int year,
        Weight tareWeight,
        Weight carryingCapacity,
        DateOnly roadworthyExpiry,
        DateOnly licenceExpiry)
        : base(id)
    {
        TenantId = tenantId;
        BranchId = branchId;
        Registration = registration;
        FleetNumber = fleetNumber;
        Make = make;
        Model = model;
        Year = year;
        TareWeight = tareWeight;
        CarryingCapacity = carryingCapacity;
        RoadworthyExpiry = roadworthyExpiry;
        LicenceExpiry = licenceExpiry;
        IsActive = true;
        IsAvailable = true;
    }

    private Vehicle()
    {
        Registration = string.Empty;
        FleetNumber = string.Empty;
        Make = string.Empty;
        Model = string.Empty;
        TareWeight = null!;
        CarryingCapacity = null!;
    }

    public TenantId TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public string Registration { get; private set; }

    public string FleetNumber { get; private set; }

    public string Make { get; private set; }

    public string Model { get; private set; }

    public int Year { get; private set; }

    public Weight TareWeight { get; private set; }

    public Weight CarryingCapacity { get; private set; }

    public DateOnly RoadworthyExpiry { get; private set; }

    public DateOnly LicenceExpiry { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsAvailable { get; private set; }

    public static Result<Vehicle> Create(
        Guid id,
        TenantId tenantId,
        Guid branchId,
        string registration,
        string fleetNumber,
        string make,
        string model,
        int year,
        Weight tareWeight,
        Weight carryingCapacity,
        DateOnly roadworthyExpiry,
        DateOnly licenceExpiry)
    {
        var error = DomainValidation.Required(
                registration,
                "vehicle.registration",
                30)
            ?? DomainValidation.Required(
                fleetNumber,
                "vehicle.fleet_number",
                40)
            ?? DomainValidation.Required(make, "vehicle.make", 80)
            ?? DomainValidation.Required(model, "vehicle.model", 80);

        if (id == Guid.Empty || tenantId.IsEmpty || branchId == Guid.Empty)
        {
            error = new DomainError(
                "vehicle.identifiers_required",
                "Vehicle, branch and tenant identifiers are required.");
        }
        else if (year is < 1900 or > 3000)
        {
            error = new DomainError(
                "vehicle.invalid_year",
                "Vehicle year is outside the supported range.");
        }
        else if (carryingCapacity.Value <= 0m)
        {
            error = new DomainError(
                "vehicle.invalid_capacity",
                "Vehicle carrying capacity must be greater than zero.");
        }

        return error is not null
            ? Result<Vehicle>.Failure(error)
            : Result<Vehicle>.Success(new Vehicle(
                id,
                tenantId,
                branchId,
                registration.Trim().ToUpperInvariant(),
                fleetNumber.Trim().ToUpperInvariant(),
                make.Trim(),
                model.Trim(),
                year,
                tareWeight,
                carryingCapacity,
                roadworthyExpiry,
                licenceExpiry));
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;

    public Result Update(
        Guid branchId,
        string registration,
        string fleetNumber,
        string make,
        string model,
        int year,
        Weight tareWeight,
        Weight carryingCapacity,
        DateOnly roadworthyExpiry,
        DateOnly licenceExpiry,
        bool isAvailable)
    {
        var candidate = Create(
            Id,
            TenantId,
            branchId,
            registration,
            fleetNumber,
            make,
            model,
            year,
            tareWeight,
            carryingCapacity,
            roadworthyExpiry,
            licenceExpiry);
        if (candidate.IsFailure)
        {
            return Result.Failure(candidate.Error!);
        }

        BranchId = candidate.Value.BranchId;
        Registration = candidate.Value.Registration;
        FleetNumber = candidate.Value.FleetNumber;
        Make = candidate.Value.Make;
        Model = candidate.Value.Model;
        Year = candidate.Value.Year;
        TareWeight = candidate.Value.TareWeight;
        CarryingCapacity = candidate.Value.CarryingCapacity;
        RoadworthyExpiry = candidate.Value.RoadworthyExpiry;
        LicenceExpiry = candidate.Value.LicenceExpiry;
        IsAvailable = isAvailable;
        return Result.Success();
    }
}
