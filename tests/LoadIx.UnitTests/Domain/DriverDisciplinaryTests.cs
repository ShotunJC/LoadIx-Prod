using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Fleet;

namespace LoadIx.UnitTests.Domain;

public sealed class DriverDisciplinaryTests
{
    private static readonly TenantId Tenant =
        TenantId.From(Guid.Parse("70000000-0000-0000-0000-000000000001"));
    private static readonly Guid DriverId = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid UserId = Guid.Parse("70000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesDriverWarningSuccessfully()
    {
        var result = DriverWarning.Create(
            Guid.NewGuid(),
            Tenant,
            DriverId,
            "WRN-2026-001",
            DriverWarningType.FirstWritten,
            DriverWarningCategory.Speeding,
            Now.AddDays(-1),
            Now,
            Now.AddMonths(6),
            "Exceeded maximum speed limit by 25 km/h on N3",
            UserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.WarningNumber.Should().Be("WRN-2026-001");
        result.Value.Status.Should().Be(DriverWarningStatus.Active);
        result.Value.WarningType.Should().Be(DriverWarningType.FirstWritten);
        result.Value.Category.Should().Be(DriverWarningCategory.Speeding);
    }

    [Fact]
    public void RejectsWarningWithExpiryBeforeIssueDate()
    {
        var result = DriverWarning.Create(
            Guid.NewGuid(),
            Tenant,
            DriverId,
            "WRN-2026-002",
            DriverWarningType.Verbal,
            DriverWarningCategory.LateDelivery,
            Now,
            Now,
            Now.AddDays(-1),
            "Arrived 4 hours late without notification",
            UserId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("driver_warning.invalid_expiry");
    }

    [Fact]
    public void RevokesActiveWarningWithReason()
    {
        var warning = DriverWarning.Create(
            Guid.NewGuid(),
            Tenant,
            DriverId,
            "WRN-2026-003",
            DriverWarningType.SecondWritten,
            DriverWarningCategory.DieselLoss,
            Now.AddDays(-1),
            Now,
            Now.AddMonths(6),
            "Discrepancy in fuel tank dip",
            UserId).Value;

        var revokeTime = Now.AddDays(2);
        var revokeResult = warning.Revoke("Fuel sensor calibration fault confirmed by workshop", UserId, revokeTime);

        revokeResult.IsSuccess.Should().BeTrue();
        warning.Status.Should().Be(DriverWarningStatus.Revoked);
        warning.RevocationReason.Should().Be("Fuel sensor calibration fault confirmed by workshop");
        warning.RevokedByUserId.Should().Be(UserId);
        warning.RevokedAtUtc.Should().Be(revokeTime);
    }

    [Fact]
    public void DriverWarningExpiresWhenNowPassesExpiryDate()
    {
        var warning = DriverWarning.Create(
            Guid.NewGuid(),
            Tenant,
            DriverId,
            "WRN-2026-004",
            DriverWarningType.Verbal,
            DriverWarningCategory.VehicleAbuse,
            Now.AddMonths(-7),
            Now.AddMonths(-6),
            Now.AddMonths(-1),
            "Harsh braking incident",
            UserId).Value;

        warning.CheckAndApplyExpiry(Now);
        warning.Status.Should().Be(DriverWarningStatus.Expired);
    }

    [Fact]
    public void CreatesDriverDeductionSuccessfully()
    {
        var result = DriverDeduction.Create(
            Guid.NewGuid(),
            Tenant,
            DriverId,
            "DED-2026-001",
            DriverDeductionCategory.TrafficFine,
            "ZAR",
            totalAmount: 1500m,
            installmentAmount: 500m,
            effectiveDate: DateOnly.FromDateTime(Now.DateTime),
            description: "Traffic camera fine on R21",
            createdAtUtc: Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeductionNumber.Should().Be("DED-2026-001");
        result.Value.TotalAmount.Should().Be(1500m);
        result.Value.InstallmentAmount.Should().Be(500m);
        result.Value.RemainingBalance.Should().Be(1500m);
        result.Value.Status.Should().Be(DriverDeductionStatus.Draft);
    }

    [Fact]
    public void RejectsDeductionWhereInstallmentExceedsTotal()
    {
        var result = DriverDeduction.Create(
            Guid.NewGuid(),
            Tenant,
            DriverId,
            "DED-2026-002",
            DriverDeductionCategory.VehicleDamage,
            "ZAR",
            totalAmount: 1000m,
            installmentAmount: 1500m,
            effectiveDate: DateOnly.FromDateTime(Now.DateTime),
            description: "Mirror replacement",
            createdAtUtc: Now);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("driver_deduction.invalid_installment");
    }

    [Fact]
    public void ApprovesAndAppliesInstallmentsUntilCompleted()
    {
        var deduction = DriverDeduction.Create(
            Guid.NewGuid(),
            Tenant,
            DriverId,
            "DED-2026-003",
            DriverDeductionCategory.LoanRepayment,
            "ZAR",
            totalAmount: 1000m,
            installmentAmount: 500m,
            effectiveDate: DateOnly.FromDateTime(Now.DateTime),
            description: "Salary advance recovery",
            createdAtUtc: Now).Value;

        deduction.Approve(UserId, Now).IsSuccess.Should().BeTrue();
        deduction.Status.Should().Be(DriverDeductionStatus.Active);

        // First installment
        deduction.ApplyInstallment(500m).IsSuccess.Should().BeTrue();
        deduction.RemainingBalance.Should().Be(500m);
        deduction.Status.Should().Be(DriverDeductionStatus.Active);

        // Second installment completes the deduction
        deduction.ApplyInstallment(500m).IsSuccess.Should().BeTrue();
        deduction.RemainingBalance.Should().Be(0m);
        deduction.Status.Should().Be(DriverDeductionStatus.Completed);
    }
}
