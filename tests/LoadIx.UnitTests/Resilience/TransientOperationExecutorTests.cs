using System.Net;
using System.Net.Http;
using System.Net.Mail;
using FluentAssertions;
using LoadIx.Infrastructure.Resilience;
using LoadIx.Infrastructure.Seed;

namespace LoadIx.UnitTests.Resilience;

public sealed class TransientOperationExecutorTests
{
    [Fact]
    public void TransportFailuresAreTransientButBusinessFailuresAreNot()
    {
        TransientOperationExecutor.IsTransient(new TimeoutException()).Should().BeTrue();
        TransientOperationExecutor.IsTransient(new HttpRequestException()).Should().BeTrue();
        TransientOperationExecutor.IsTransient(new IOException()).Should().BeTrue();
        TransientOperationExecutor.IsTransient(new InvalidOperationException()).Should().BeFalse();
        TransientOperationExecutor.IsTransient(new ProviderCircuitOpenException("smtp"))
            .Should().BeFalse();
    }

    [Fact]
    public void HttpRetriesAreLimitedToTimeoutThrottlingAndServerFailures()
    {
        TransientOperationExecutor.IsTransient(
            new HttpRequestException("timeout", null, HttpStatusCode.RequestTimeout))
            .Should().BeTrue();
        TransientOperationExecutor.IsTransient(
            new HttpRequestException("throttled", null, HttpStatusCode.TooManyRequests))
            .Should().BeTrue();
        TransientOperationExecutor.IsTransient(
            new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable))
            .Should().BeTrue();
        TransientOperationExecutor.IsTransient(
            new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized))
            .Should().BeFalse();
        TransientOperationExecutor.IsTransient(
            new HttpRequestException("missing", null, HttpStatusCode.NotFound))
            .Should().BeFalse();
    }

    [Fact]
    public void SmtpRetriesAreLimitedToTemporaryDeliveryFailures()
    {
        TransientOperationExecutor.IsTransient(
            new SmtpException(SmtpStatusCode.ServiceNotAvailable))
            .Should().BeTrue();
        TransientOperationExecutor.IsTransient(
            new SmtpException(SmtpStatusCode.MailboxBusy))
            .Should().BeTrue();
        TransientOperationExecutor.IsTransient(
            new SmtpException(SmtpStatusCode.MailboxUnavailable))
            .Should().BeFalse();
        TransientOperationExecutor.IsTransient(
            new SmtpException(SmtpStatusCode.ClientNotPermitted))
            .Should().BeFalse();
    }

    [Fact]
    public void CircuitBreakerOpensAfterFiveConsecutiveProviderFailures()
    {
        var breaker = new ProviderCircuitBreaker(TimeProvider.System);

        breaker.RecordFailure("object-storage").Should().BeFalse();
        breaker.RecordFailure("object-storage").Should().BeFalse();
        breaker.RecordFailure("object-storage").Should().BeFalse();
        breaker.RecordFailure("object-storage").Should().BeFalse();
        breaker.RecordFailure("object-storage").Should().BeTrue();
        breaker.TryEnter("object-storage").Should().BeFalse();
    }

    [Fact]
    public async Task PermanentHalfOpenFailureReleasesProbe()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var breaker = new ProviderCircuitBreaker(timeProvider);
        var executor = new TransientOperationExecutor(
            breaker,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TransientOperationExecutor>.Instance);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            breaker.RecordFailure("smtp");
        }

        timeProvider.Advance(TimeSpan.FromSeconds(31));
        await executor.Invoking(executorInstance => executorInstance.ExecuteAsync(
                "smtp",
                _ => Task.FromException(new InvalidOperationException("permanent")),
                CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>();

        breaker.TryEnter("smtp").Should().BeTrue();
    }

    [Fact]
    public void PerformanceSeedPlanMatchesThePilotWorkload()
    {
        PerformanceSeedPlan.TenantCount.Should().Be(10);
        PerformanceSeedPlan.UserCount.Should().Be(100);
        PerformanceSeedPlan.DriverCount.Should().Be(1_000);
        PerformanceSeedPlan.VehicleCount.Should().Be(1_000);
        PerformanceSeedPlan.LoadInstructionCount.Should().Be(10_000);
        PerformanceSeedPlan.TripCount.Should().Be(100_000);
        (PerformanceSeedPlan.UserCount % PerformanceSeedPlan.TenantCount).Should().Be(0);
        (PerformanceSeedPlan.TripCount % PerformanceSeedPlan.TenantCount).Should().Be(0);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan duration) => currentUtcNow += duration;
    }
}
