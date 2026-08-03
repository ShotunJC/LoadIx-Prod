using System.Text;
using System.Text.Json;
using LoadIx.Application.Reporting;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LoadIx.Infrastructure.Reporting;

public interface IReportExportPublisher
{
    Task PublishAsync(Guid jobId, TenantId tenantId, CancellationToken cancellationToken);
}

public sealed class RabbitMqReportExportPublisher(
    IOptions<RabbitMqOptions> options) : IReportExportPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(Guid jobId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new ExportMessage(jobId, tenantId.Value)));
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options.ReportExportQueueName,
            mandatory: false,
            basicProperties: new BasicProperties { Persistent = true, ContentType = "application/json" },
            body: body,
            cancellationToken);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return _channel;
        var factory = new ConnectionFactory
        {
            HostName = _options.RabbitMqHost,
            Port = _options.RabbitMqPort,
            UserName = _options.RabbitMqUsername,
            Password = _options.RabbitMqPassword,
            VirtualHost = _options.RabbitMqVirtualHost,
        };
        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await DeclareQueueAsync(_channel, _options.ReportExportQueueName, cancellationToken);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    internal static Task DeclareQueueAsync(IChannel channel, string queueName, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

    private sealed record ExportMessage(Guid JobId, Guid TenantId);
}

public sealed class ReportExportService(
    LoadIxDbContext db,
    TimeProvider timeProvider,
    IReportExportPublisher publisher,
    ILogger<ReportExportService> logger) : IReportExportService
{
    private const int MaximumAttempts = 3;
    private static readonly Action<ILogger, Guid, string, Exception?> PublishFailed =
        LoggerMessage.Define<Guid, string>(LogLevel.Warning, new EventId(1501, "ReportExportPublishFailed"), "Report export job {JobId} could not be published with {ExceptionType}");

    public async Task<ReportExportJobResponse> EnqueueAsync(
        string report,
        ReportingFilter filter,
        TenantId tenantId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var job = new ReportExportJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Report = report,
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            BranchId = filter.BranchId,
            CustomerId = filter.CustomerId,
            Page = 1,
            PageSize = 100,
            CreatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime(),
            Status = ReportExportJobStatus.Queued,
        };
        db.ReportExportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        await TryPublishAsync(job, cancellationToken);
        return job.ToResponse();
    }

    public ReportExportJobResponse? GetJob(Guid jobId, TenantId tenantId) =>
        db.ReportExportJobs.AsNoTracking().SingleOrDefault(item => item.Id == jobId && item.TenantId == tenantId)?.ToResponse();

    public byte[]? GetContent(Guid jobId, TenantId tenantId) =>
        db.ReportExportJobs.AsNoTracking()
            .Where(item => item.Id == jobId && item.TenantId == tenantId && item.Status == ReportExportJobStatus.Completed)
            .Select(item => item.Content).SingleOrDefault();

    internal async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        await db.ReportExportJobs.IgnoreQueryFilters()
            .Where(item => item.TenantId.Value != Guid.Empty && item.Status == ReportExportJobStatus.Processing
                && item.LeaseExpiresAtUtc < now && item.Attempts >= MaximumAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ReportExportJobStatus.Failed)
                .SetProperty(item => item.Error, "Processing lease expired after the maximum retry count."), cancellationToken);
        await db.ReportExportJobs.IgnoreQueryFilters()
            .Where(item => item.TenantId.Value != Guid.Empty && item.Status == ReportExportJobStatus.Processing
                && item.LeaseExpiresAtUtc < now && item.Attempts < MaximumAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ReportExportJobStatus.Queued)
                .SetProperty(item => item.Error, "Processing lease expired; the export was requeued."), cancellationToken);

        var jobs = await db.ReportExportJobs.IgnoreQueryFilters()
            .Where(item => item.TenantId.Value != Guid.Empty && item.Status == ReportExportJobStatus.Queued
                && (item.LastPublishedAtUtc == null || item.LastPublishedAtUtc < now.AddSeconds(-30))
                && item.Attempts < MaximumAttempts)
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id).Take(50).ToListAsync(cancellationToken);
        foreach (var job in jobs) await TryPublishAsync(job, cancellationToken);
    }

    private async Task TryPublishAsync(ReportExportJob job, CancellationToken cancellationToken)
    {
        try
        {
            await publisher.PublishAsync(job.Id, job.TenantId, cancellationToken);
            job.LastPublishedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishFailed(logger, job.Id, exception.GetType().Name, null);
        }
    }
}

public sealed class ReportExportWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    TimeProvider timeProvider,
    ILogger<ReportExportWorker> logger) : BackgroundService
{
    private const int MaximumAttempts = 3;
    private const int MaximumBackgroundRows = 100_000;
    private const int BatchSize = 100;
    private static readonly Action<ILogger, ulong, string, Exception?> ProcessingFailed =
        LoggerMessage.Define<ulong, string>(LogLevel.Error, new EventId(1502, "ReportExportProcessingFailed"), "Report export delivery {DeliveryTag} failed with {ExceptionType}");
    private static readonly Action<ILogger, string, Exception?> ConnectionFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1503, "ReportExportConnectionFailed"), "Report export worker connection failed with {ExceptionType}; retrying");
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ConsumeAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                ConnectionFailed(logger, exception.GetType().Name, null);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.RabbitMqHost,
            Port = _options.RabbitMqPort,
            UserName = _options.RabbitMqUsername,
            Password = _options.RabbitMqPassword,
            VirtualHost = _options.RabbitMqVirtualHost,
        };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqReportExportPublisher.DeclareQueueAsync(channel, _options.ReportExportQueueName, cancellationToken);
        await channel.BasicQosAsync(0, 1, false, cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<ExportMessage>(args.Body.Span)
                    ?? throw new InvalidOperationException("Invalid report export message.");
                await ProcessAsync(message, cancellationToken);
                await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                ProcessingFailed(logger, args.DeliveryTag, exception.GetType().Name, null);
                await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken);
            }
        };
        await channel.BasicConsumeAsync(_options.ReportExportQueueName, autoAck: false, consumer, cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private async Task ProcessAsync(ExportMessage message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CurrentRequestContext>();
        var tenantId = TenantId.From(message.TenantId);
        context.Set(tenantId, null);
        var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IReportingService>();
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var claimed = await db.ReportExportJobs.Where(item => item.Id == message.JobId
                && item.TenantId == tenantId && item.Status == ReportExportJobStatus.Queued
                && item.Attempts < MaximumAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ReportExportJobStatus.Processing)
                .SetProperty(item => item.StartedAtUtc, now)
                .SetProperty(item => item.LastHeartbeatAtUtc, now)
                .SetProperty(item => item.LeaseExpiresAtUtc, now.AddMinutes(2))
                .SetProperty(item => item.Attempts, item => item.Attempts + 1), cancellationToken);
        if (claimed == 0) return;

        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"loadix-report-export-{message.JobId:N}-{Guid.NewGuid():N}.csv");
        try
        {
            var job = await db.ReportExportJobs.SingleAsync(item => item.Id == message.JobId && item.TenantId == tenantId, cancellationToken);
            var page = 1;
            var totalRows = 0;
            await using (var content = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (true)
                {
                    var batch = await service.ExportCsvBatchAsync(job.Report, job.ToFilter() with { Page = page, PageSize = BatchSize }, page == 1, cancellationToken);
                    if (page == 1 && batch.TotalCount > MaximumBackgroundRows)
                    {
                        throw new ReportExportTooLargeException(MaximumBackgroundRows, batch.TotalCount);
                    }
                    await content.WriteAsync(batch.Content, cancellationToken);
                    totalRows += batch.RowCount;
                    await db.ReportExportJobs.Where(item => item.Id == job.Id && item.TenantId == tenantId && item.Status == ReportExportJobStatus.Processing)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(item => item.LastHeartbeatAtUtc, timeProvider.GetUtcNow().ToUniversalTime())
                            .SetProperty(item => item.LeaseExpiresAtUtc, timeProvider.GetUtcNow().ToUniversalTime().AddMinutes(2)), cancellationToken);
                    if (batch.RowCount == 0 || totalRows >= batch.TotalCount) break;
                    page++;
                }
            }

            // The current job contract stores completed content in varbinary(max).
            // Generate into a sequential temporary file first so the worker does
            // not retain the complete export in a growing managed buffer.
            job.Content = await File.ReadAllBytesAsync(temporaryPath, cancellationToken);
            job.RowCount = totalRows;
            job.ContentLengthBytes = job.Content.LongLength;
            job.Status = ReportExportJobStatus.Completed;
            job.CompletedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            job.LastHeartbeatAtUtc = job.CompletedAtUtc;
            job.LeaseExpiresAtUtc = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var job = await db.ReportExportJobs.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == message.JobId && item.TenantId == tenantId, cancellationToken);
            job.Status = job.Attempts >= MaximumAttempts ? ReportExportJobStatus.Failed : ReportExportJobStatus.Queued;
            job.Error = job.Attempts >= MaximumAttempts
                ? "Export generation failed after the maximum retry count."
                : "Export generation failed and will be retried.";
            job.LastHeartbeatAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            job.LeaseExpiresAtUtc = null;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // A failed cleanup is harmless; the OS temporary-file policy
                // remains responsible for eventual removal.
            }
        }
    }

    private sealed record ExportMessage(Guid JobId, Guid TenantId);
}

public sealed class ReportExportRepublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<ReportExportRepublisher> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> RepublishFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1504, "ReportExportRepublishFailed"), "Report export republish cycle failed with {ExceptionType}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ReportExportService>().PublishPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                RepublishFailed(logger, exception.GetType().Name, null);
            }
        }
    }
}
