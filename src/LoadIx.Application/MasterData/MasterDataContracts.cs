namespace LoadIx.Application.MasterData;

public sealed record MasterDataQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Sort = null,
    string Direction = "asc",
    string Status = "active",
    Guid? BranchId = null,
    Guid? CustomerId = null);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AddressModel(
    string Line1,
    string? Line2,
    string City,
    string ProvinceOrRegion,
    string PostalCode,
    string CountryCode);

public sealed record ContactModel(
    Guid? Id,
    string Name,
    string? Email,
    string? PhoneNumber,
    string Category,
    bool IsActive = true);

public abstract record MasterDataItem(
    Guid Id,
    string Reference,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc);

public sealed record BranchItem(
    Guid Id,
    string Reference,
    string Name,
    AddressModel Address,
    string? ContactEmail,
    string? ContactPhoneNumber,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc)
    : MasterDataItem(Id, Reference, IsActive, Version, ModifiedAtUtc);

public sealed record CustomerItem(
    Guid Id,
    string Reference,
    string LegalName,
    string TradingName,
    string RegistrationNumber,
    string? TaxNumber,
    int PaymentTermsDays,
    AddressModel BillingAddress,
    IReadOnlyList<ContactModel> Contacts,
    string? Notes,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc)
    : MasterDataItem(Id, Reference, IsActive, Version, ModifiedAtUtc);

public sealed record LocationItem(
    Guid Id,
    string Reference,
    Guid? CustomerId,
    string? CustomerName,
    string Name,
    string Type,
    AddressModel Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal? GeofenceRadiusMetres,
    string? SiteInstructions,
    string? ContactName,
    string? ContactPhoneNumber,
    string? OperatingHours,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc)
    : MasterDataItem(Id, Reference, IsActive, Version, ModifiedAtUtc);

public sealed record CommodityItem(
    Guid Id,
    string Reference,
    string Name,
    string DefaultWeightUnit,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc)
    : MasterDataItem(Id, Reference, IsActive, Version, ModifiedAtUtc);

public sealed record DriverItem(
    Guid Id,
    string Reference,
    Guid BranchId,
    string BranchName,
    string FirstName,
    string LastName,
    string DriverType,
    string IdentificationReference,
    string MobileNumber,
    string LicenceNumber,
    DateOnly LicenceExpiry,
    string LicenceStatus,
    string EmergencyContactName,
    string EmergencyContactNumber,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc)
    : MasterDataItem(Id, Reference, IsActive, Version, ModifiedAtUtc);

public sealed record VehicleItem(
    Guid Id,
    string Reference,
    Guid BranchId,
    string BranchName,
    string Registration,
    string Make,
    string Model,
    int Year,
    decimal TareWeight,
    decimal CarryingCapacity,
    string WeightUnit,
    DateOnly RoadworthyExpiry,
    DateOnly LicenceExpiry,
    string ComplianceStatus,
    bool IsAvailable,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc)
    : MasterDataItem(Id, Reference, IsActive, Version, ModifiedAtUtc);

public sealed record TrailerItem(
    Guid Id,
    string Reference,
    Guid BranchId,
    string BranchName,
    string Registration,
    string Make,
    string Model,
    int Year,
    decimal TareWeight,
    decimal CarryingCapacity,
    string WeightUnit,
    DateOnly LicenceExpiry,
    string ComplianceStatus,
    bool IsAvailable,
    bool IsActive,
    string Version,
    DateTimeOffset ModifiedAtUtc)
    : MasterDataItem(Id, Reference, IsActive, Version, ModifiedAtUtc);

public sealed record BranchWriteModel(
    string Name,
    string Code,
    AddressModel Address,
    string? ContactEmail,
    string? ContactPhoneNumber,
    string? Version = null);

public sealed record CustomerWriteModel(
    string LegalName,
    string TradingName,
    string RegistrationNumber,
    string? TaxNumber,
    int PaymentTermsDays,
    AddressModel BillingAddress,
    IReadOnlyList<ContactModel> Contacts,
    string? Notes,
    string? Version = null);

public sealed record LocationWriteModel(
    Guid? CustomerId,
    string Name,
    string Code,
    string Type,
    AddressModel Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal? GeofenceRadiusMetres,
    string? SiteInstructions,
    string? ContactName,
    string? ContactPhoneNumber,
    string? OperatingHours,
    string? Version = null);

public sealed record CommodityWriteModel(
    string Name,
    string Code,
    string DefaultWeightUnit,
    string? Version = null);

public sealed record DriverWriteModel(
    Guid BranchId,
    string FirstName,
    string LastName,
    string DriverType,
    string IdentificationReference,
    string MobileNumber,
    string LicenceNumber,
    DateOnly LicenceExpiry,
    string EmergencyContactName,
    string EmergencyContactNumber,
    string? Version = null);

public sealed record VehicleWriteModel(
    Guid BranchId,
    string Registration,
    string FleetNumber,
    string Make,
    string Model,
    int Year,
    decimal TareWeight,
    decimal CarryingCapacity,
    string WeightUnit,
    DateOnly RoadworthyExpiry,
    DateOnly LicenceExpiry,
    bool IsAvailable,
    string? Version = null);

public sealed record TrailerWriteModel(
    Guid BranchId,
    string Registration,
    string FleetNumber,
    string Make,
    string Model,
    int Year,
    decimal TareWeight,
    decimal CarryingCapacity,
    string WeightUnit,
    DateOnly LicenceExpiry,
    bool IsAvailable,
    string? Version = null);

public enum MasterDataFailure
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
}

public sealed record MasterDataResult<T>(
    T? Value,
    MasterDataFailure Failure,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public bool IsSuccess => Failure == MasterDataFailure.None;

    public static MasterDataResult<T> Success(T value) =>
        new(value, MasterDataFailure.None, string.Empty, string.Empty);

    public static MasterDataResult<T> Fail(
        MasterDataFailure failure,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(default, failure, code, message, errors);
}
