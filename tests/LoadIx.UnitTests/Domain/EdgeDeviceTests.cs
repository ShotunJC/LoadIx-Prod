using System.Globalization;
using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Edge;

namespace LoadIx.UnitTests.Domain;

public sealed class EdgeDeviceTests
{
    [Fact]
    public void RevokedCredentialCannotBeUsed()
    {
        var now = DateTimeOffset.Parse(
            "2026-08-01T10:00:00Z",
            CultureInfo.InvariantCulture);
        var result = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            TenantId.New(),
            Guid.NewGuid(),
            EdgeDeviceCredentialType.Device,
            "hashed-secret",
            now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Revoke(now.AddMinutes(1));

        result.Value.IsUsableAt(now.AddMinutes(2)).Should().BeFalse();
    }

    [Fact]
    public void PendingReplacementExpiresUntilConfirmedAndCanBeActivated()
    {
        var now = DateTimeOffset.Parse(
            "2026-08-01T10:00:00Z",
            CultureInfo.InvariantCulture);
        var previousId = Guid.NewGuid();
        var result = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            TenantId.New(),
            Guid.NewGuid(),
            EdgeDeviceCredentialType.Device,
            "hashed-secret",
            now,
            replacedCredentialId: previousId,
            activatedAtUtc: now,
            overlapExpiresAtUtc: now.AddMinutes(5),
            status: EdgeDeviceCredentialStatus.PendingConfirmation);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EdgeDeviceCredentialStatus.PendingConfirmation);
        result.Value.IsUsableAt(now.AddMinutes(4)).Should().BeTrue();
        result.Value.IsUsableAt(now.AddMinutes(6)).Should().BeFalse();
        result.Value.Confirm(now.AddMinutes(6)).IsFailure.Should().BeTrue();

        result.Value.Confirm(now.AddMinutes(1)).IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EdgeDeviceCredentialStatus.Active);
        result.Value.ConfirmedAtUtc.Should().Be(now.AddMinutes(1));
        result.Value.IsUsableAt(now.AddDays(1)).Should().BeTrue();
    }

    [Fact]
    public void StableReadingRetainsItsRawReadingReference()
    {
        var tenantId = TenantId.New();
        var rawId = Guid.NewGuid();
        var result = StableWeightReading.Create(
            Guid.NewGuid(),
            tenantId,
            rawId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1000m,
            WeightUnit.Kilograms,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "SIM-001",
            "correlation-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.RawWeightReadingId.Should().Be(rawId);
        result.Value.Unit.Should().Be(WeightUnit.Kilograms);
    }
}
