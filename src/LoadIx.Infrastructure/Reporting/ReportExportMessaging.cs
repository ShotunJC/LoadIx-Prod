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
        await _channel.QueueDeclareAsync(_options.ReportExportQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private sealed record ExportMessage(Guid JobId, Guid TenantId);
}

public sealed class ReportExportService(
    LoadIxDbContext db,
    TimeProvider timeProvider,
    IReportExportPublisher publisher,
    ILogger<ReportExportService> logger) : IReportExportService
{
    private static readonly Action<ILogger, Guid, Exception?> PublishFailed =
        LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(1501, "ReportExportPublishFailed"), "Report export job {JobId} could not be published");

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
            FromUtc = filter.FromUtc,
            ToUtc = filter.ToUtc,
            BranchId = filter.BranchId,
            CustomerId = filter.CustomerId,
            Page = 1,
            PageSize = 5_000,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };
        db.ReportExportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        await TryPublishAsync(job, cancellationToken);
        return job.ToResponse();
    }

    public ReportExportJobResponse? GetJob(Guid jobId, TenantId tenantId) =>
        db.ReportExportJobs.AsNoTracking().SingleOrDefault(item => item.Id == jobId && item.TenantId == tenantId)?.ToResponse();

    public byte[]? GetContent(Guid jobId, TenantId tenantId) =>
        db.ReportExportJobs.AsNoTracking().Where(item => item.Id == jobId && item.TenantId == tenantId && item.Status == "Completed").Select(item => item.Content).SingleOrDefault();

    internal async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        var jobs = await db.ReportExportJobs
            .IgnoreQueryFilters()
            .Where(item => item.Status == "Queued" && (item.LastPublishedAtUtc == null || item.LastPublishedAtUtc < timeProvider.GetUtcNow().AddSeconds(-30)))
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id).Take(50).ToListAsync(cancellationToken);
        foreach (var job in jobs) await TryPublishAsync(job, cancellationToken);
    }

    private async Task TryPublishAsync(ReportExportJob job, CancellationToken cancellationToken)
    {
        try
        {
            await publisher.PublishAsync(job.Id, job.TenantId, cancellationToken);
            job.LastPublishedAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishFailed(logger, job.Id, exception);
        }
    }
}

public sealed class ReportExportWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    TimeProvider timeProvider,
    ILogger<ReportExportWorker> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;
    private static readonly Action<ILogger, ulong, Exception?> ProcessingFailed =
        LoggerMessage.Define<ulong>(LogLevel.Error, new EventId(1502, "ReportExportProcessingFailed"), "Report export delivery {DeliveryTag} failed");
    private static readonly Action<ILogger, Exception?> ConnectionFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1503, "ReportExportConnectionFailed"), "Report export worker connection failed; retrying");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                ConnectionFailed(logger, exception);
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
        await channel.QueueDeclareAsync(_options.ReportExportQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
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
                ProcessingFailed(logger, args.DeliveryTag, exception);
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
        context.Set(TenantId.From(message.TenantId), null);
        var db = scope.ServiceProvider.GetRequiredService<LoadIxDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IReportingService>();
        var claimed = await db.ReportExportJobs.Where(item => item.Id == message.JobId && item.Status == "Queued")
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, "Processing").SetProperty(item => item.Attempts, item => item.Attempts + 1), cancellationToken);
        if (claimed == 0) return;
        try
        {
            var job = await db.ReportExportJobs.SingleAsync(item => item.Id == message.JobId, cancellationToken);
            job.Content = await service.ExportCsvAsync(job.Report, job.ToFilter(), cancellationToken);
            job.Status = "Completed";
            job.CompletedAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            var job = await db.ReportExportJobs.IgnoreQueryFilters().SingleAsync(item => item.Id == message.JobId, cancellationToken);
            job.Status = "Failed";
            job.Error = "Export generation failed.";
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private sealed record ExportMessage(Guid JobId, Guid TenantId);
}

public sealed class ReportExportRepublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<ReportExportRepublisher> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> RepublishFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1504, "ReportExportRepublishFailed"), "Report export republish cycle failed");
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
                RepublishFailed(logger, exception);
            }
        }
    }
}
