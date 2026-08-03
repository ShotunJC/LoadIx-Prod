using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace LoadIx.Infrastructure.Resilience;

public sealed class ProviderCircuitOpenException(string provider)
    : Exception($"The {provider} provider circuit is open.")
{
    public string Provider { get; } = provider;
}

public sealed class ProviderCircuitBreaker(TimeProvider timeProvider)
{
    private const int FailureThreshold = 5;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, CircuitState> states = new(
        StringComparer.Ordinal);

    public bool TryEnter(string provider)
    {
        var state = states.GetOrAdd(provider, static _ => new CircuitState());
        lock (state.SyncRoot)
        {
            var now = timeProvider.GetUtcNow();
            if (state.OpenUntilUtc is not { } openUntil)
            {
                return true;
            }

            if (openUntil > now)
            {
                return false;
            }

            if (state.HalfOpenProbeInProgress)
            {
                return false;
            }

            state.HalfOpenProbeInProgress = true;
            return true;
        }
    }

    public void RecordSuccess(string provider)
    {
        var state = states.GetOrAdd(provider, static _ => new CircuitState());
        lock (state.SyncRoot)
        {
            state.ConsecutiveFailures = 0;
            state.OpenUntilUtc = null;
            state.HalfOpenProbeInProgress = false;
        }
    }

    public bool RecordFailure(string provider)
    {
        var state = states.GetOrAdd(provider, static _ => new CircuitState());
        lock (state.SyncRoot)
        {
            state.ConsecutiveFailures++;
            state.HalfOpenProbeInProgress = false;
            if (state.ConsecutiveFailures < FailureThreshold)
            {
                return false;
            }

            state.OpenUntilUtc = timeProvider.GetUtcNow().Add(OpenDuration);
            return true;
        }
    }

    public void ReleaseProbe(string provider)
    {
        var state = states.GetOrAdd(provider, static _ => new CircuitState());
        lock (state.SyncRoot)
        {
            state.HalfOpenProbeInProgress = false;
        }
    }

    private sealed class CircuitState
    {
        public object SyncRoot { get; } = new();
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? OpenUntilUtc { get; set; }
        public bool HalfOpenProbeInProgress { get; set; }
    }
}

public sealed class TransientOperationExecutor(
    ProviderCircuitBreaker circuitBreaker,
    ILogger<TransientOperationExecutor> logger)
{
    private const int MaximumAttempts = 3;

    private static readonly Action<ILogger, string, int, double, Exception?> RetryAttempt =
        LoggerMessage.Define<string, int, double>(
            LogLevel.Warning,
            new EventId(1601, "TransientProviderRetry"),
            "Transient {Provider} operation failed on attempt {Attempt}; retrying after {DelayMilliseconds} ms");

    public Task ExecuteAsync(
        string provider,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            provider,
            async token =>
            {
                await operation(token);
                return true;
            },
            cancellationToken);

    public async Task<T> ExecuteAsync<T>(
        string provider,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!circuitBreaker.TryEnter(provider))
            {
                throw new ProviderCircuitOpenException(provider);
            }

            try
            {
                var result = await operation(cancellationToken);
                circuitBreaker.RecordSuccess(provider);
                return result;
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                circuitBreaker.ReleaseProbe(provider);
                throw;
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                var opened = circuitBreaker.RecordFailure(provider);
                if (opened || attempt == MaximumAttempts)
                {
                    throw;
                }

                var delay = Backoff(attempt);
                RetryAttempt(
                    logger,
                    provider,
                    attempt,
                    delay.TotalMilliseconds,
                    null);
                await Task.Delay(delay, cancellationToken);
            }
            catch
            {
                // Validation, authentication, authorization, and other
                // permanent provider failures must not be retried or counted
                // as transport failures.
                circuitBreaker.ReleaseProbe(provider);
                throw;
            }
        }

        throw new UnreachableException();
    }

    public static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ProviderCircuitOpenException)
            {
                return false;
            }

            if (current is HttpRequestException httpRequestException)
            {
                return httpRequestException.StatusCode is null
                    || httpRequestException.StatusCode == HttpStatusCode.RequestTimeout
                    || httpRequestException.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)httpRequestException.StatusCode >= 500;
            }

            if (current is SmtpException smtpException)
            {
                return smtpException.StatusCode is SmtpStatusCode.GeneralFailure
                    or SmtpStatusCode.ServiceNotAvailable
                    or SmtpStatusCode.MailboxBusy
                    or SmtpStatusCode.LocalErrorInProcessing
                    or SmtpStatusCode.InsufficientStorage;
            }

            if (current is TimeoutException
                or TaskCanceledException
                or SocketException
                or IOException)
            {
                return true;
            }

            // MinIO and RabbitMQ use provider-specific exception types. Their
            // transport errors are not part of the shared abstraction, so use
            // their stable type names here without coupling the policy to
            // provider internals.
            if (current.GetType().Name is "ConnectionException"
                or "BrokerUnreachableException"
                or "ConnectFailureException")
            {
                return true;
            }
        }

        return false;
    }

    private static TimeSpan Backoff(int attempt)
    {
        var baseMilliseconds = 100 * Math.Pow(2, attempt - 1);
        var jitterMilliseconds = Random.Shared.Next(0, 100);
        return TimeSpan.FromMilliseconds(Math.Min(5_000, baseMilliseconds + jitterMilliseconds));
    }
}
