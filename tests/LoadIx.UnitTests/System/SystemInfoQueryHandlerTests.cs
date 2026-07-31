using FluentAssertions;
using LoadIx.Application.System;

namespace LoadIx.UnitTests.System;

public sealed class SystemInfoQueryHandlerTests
{
    [Fact]
    public async Task HandleAsyncReturnsConfiguredSafeMetadataAndUtcTime()
    {
        var expectedTime = new DateTimeOffset(
            2026,
            7,
            28,
            8,
            30,
            0,
            TimeSpan.Zero);
        var handler = new SystemInfoQueryHandler(
            new FixedTimeProvider(expectedTime),
            new SystemInfoDescriptor("LoadIx.Api", "0.1.0", "v1"));

        var result = await handler.HandleAsync(CancellationToken.None);

        result.Should().Be(new SystemInfoSnapshot(
            "LoadIx.Api",
            "0.1.0",
            "v1",
            expectedTime));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
