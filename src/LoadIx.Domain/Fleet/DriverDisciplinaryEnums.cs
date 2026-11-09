namespace LoadIx.Domain.Fleet;

public enum DriverWarningType
{
    Verbal = 1,
    FirstWritten = 2,
    SecondWritten = 3,
    FinalWritten = 4,
}

public enum DriverWarningCategory
{
    Speeding = 1,
    DieselLoss = 2,
    CargoShortage = 3,
    VehicleAbuse = 4,
    LateDelivery = 5,
    MissingDocuments = 6,
    SafetyViolation = 7,
    Insubordination = 8,
    Other = 9,
}

public enum DriverWarningStatus
{
    Active = 1,
    Expired = 2,
    Revoked = 3,
}

public enum DriverDeductionCategory
{
    TrafficFine = 1,
    VehicleDamage = 2,
    DieselLoss = 3,
    LoanRepayment = 4,
    Cellphone = 5,
    Uniform = 6,
    Other = 7,
}

public enum DriverDeductionStatus
{
    Draft = 1,
    Approved = 2,
    Active = 3,
    Completed = 4,
    Cancelled = 5,
}
