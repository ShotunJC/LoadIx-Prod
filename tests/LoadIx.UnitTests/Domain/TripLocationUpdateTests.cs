using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Trips;

namespace LoadIx.UnitTests.Domain;

public sealed class TripLocationUpdateTests
{
    [Fact]
    public void RejectsCoordinatesOutsideThePhysicalRange()
    {
        var result = TripLocationUpdate.Create(
            Guid.NewGuid(),
            TenantId.New(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            91m,
            28m,
            10m,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("trip.location.coordinates_invalid");
    }
}
