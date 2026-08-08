using System.Security.Cryptography;
using FluentAssertions;
using LoadIx.Infrastructure.Seed;

namespace LoadIx.UnitTests.System;

public sealed class DemoSeedTests
{
    [Fact]
    public void DemoIdentifiersMeetCanonicalPilotCounts()
    {
        DevelopmentSeedIds.CustomerIds.Should().HaveCount(5).And.OnlyHaveUniqueItems();
        DevelopmentSeedIds.LocationIds.Should().HaveCount(8).And.OnlyHaveUniqueItems();
        DevelopmentSeedIds.CommodityIds.Should().HaveCount(5).And.OnlyHaveUniqueItems();
        DevelopmentSeedIds.ContractIds.Should().HaveCount(4).And.OnlyHaveUniqueItems();
        DevelopmentSeedIds.InstructionIds.Should().HaveCount(8).And.OnlyHaveUniqueItems();
        DevelopmentSeedIds.TripIds.Should().HaveCount(32).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void SamplePdfGenerationIsDeterministicAndValid()
    {
        var first = DemoPdf.Create("Synthetic POD", "No personal information");
        var second = DemoPdf.Create("Synthetic POD", "No personal information");

        first.Should().Equal(second);
        first.Should().StartWith("%PDF-1.4"u8.ToArray());
        SHA256.HashData(first).Should().Equal(SHA256.HashData(second));
    }
}
