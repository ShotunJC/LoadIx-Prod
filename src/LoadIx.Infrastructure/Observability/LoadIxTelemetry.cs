using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LoadIx.Infrastructure.Observability;

public static class LoadIxTelemetry
{
    public const string MeterName = "LoadIx";
    public const string ActivitySourceName = "LoadIx";

    public static readonly Meter Meter = new(MeterName, "0.1.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static long activeTrips;
    private static long edgeDevicesOnline;
    private static long notificationQueueDepth;
    private static long operationalOutboxDepth;
    private static long operationalOutboxDeadLetters;
    private static long reportExportQueueDepth;

    public static readonly Counter<long> BackgroundJobFailures =
        Meter.CreateCounter<long>("loadix.background.job.failures");

    public static readonly Counter<long> NotificationFailures =
        Meter.CreateCounter<long>("loadix.notification.failures");

    public static readonly Counter<long> OutboxDeadLetters =
        Meter.CreateCounter<long>("loadix.outbox.dead_letters");

    public static readonly Histogram<double> BackgroundJobDuration =
        Meter.CreateHistogram<double>("loadix.background.job.duration", "ms");

    static LoadIxTelemetry()
    {
        Meter.CreateObservableGauge(
            "loadix.active.trips",
            static () => Volatile.Read(ref activeTrips),
            description: "Current non-terminal trips across all tenants.");
        Meter.CreateObservableGauge(
            "loadix.edge.devices.online",
            static () => Volatile.Read(ref edgeDevicesOnline),
            description: "Edge devices with a recent heartbeat.");
        Meter.CreateObservableGauge(
            "loadix.notification.queue.depth",
            static () => Volatile.Read(ref notificationQueueDepth),
            description: "Pending notification deliveries.");
        Meter.CreateObservableGauge(
            "loadix.operational.outbox.depth",
            static () => Volatile.Read(ref operationalOutboxDepth),
            description: "Undispatched operational outbox messages.");
        Meter.CreateObservableGauge(
            "loadix.operational.outbox.dead_letters",
            static () => Volatile.Read(ref operationalOutboxDeadLetters),
            description: "Operational outbox messages moved to dead-letter state.");
        Meter.CreateObservableGauge(
            "loadix.report.export.queue.depth",
            static () => Volatile.Read(ref reportExportQueueDepth),
            description: "Queued and processing report exports.");
    }

    public static void SetWorkloadState(
        long activeTripCount,
        long onlineEdgeDeviceCount,
        long pendingNotificationCount,
        long pendingOutboxCount,
        long deadLetterOutboxCount,
        long queuedReportExportCount)
    {
        Volatile.Write(ref activeTrips, activeTripCount);
        Volatile.Write(ref edgeDevicesOnline, onlineEdgeDeviceCount);
        Volatile.Write(ref notificationQueueDepth, pendingNotificationCount);
        Volatile.Write(ref operationalOutboxDepth, pendingOutboxCount);
        Volatile.Write(ref operationalOutboxDeadLetters, deadLetterOutboxCount);
        Volatile.Write(ref reportExportQueueDepth, queuedReportExportCount);
    }

    public static void RecordJobFailure(string jobName) =>
        BackgroundJobFailures.Add(
            1,
            new KeyValuePair<string, object?>("job", jobName));

    public static void RecordJobDuration(string jobName, TimeSpan duration) =>
        BackgroundJobDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("job", jobName));

    public static void RecordNotificationFailure(string channel) =>
        NotificationFailures.Add(
            1,
            new KeyValuePair<string, object?>("channel", channel));

    public static void RecordOutboxDeadLetter(string eventType) =>
        OutboxDeadLetters.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType));
}
