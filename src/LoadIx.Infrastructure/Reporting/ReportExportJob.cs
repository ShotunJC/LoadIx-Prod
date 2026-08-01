using LoadIx.Application.Reporting;
using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Reporting;

public sealed class ReportExportJob
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Report { get; set; } = string.Empty;
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    // Retained for forward compatibility with jobs created by the original PR.
    // New requests never populate these UTC fields.
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? CustomerId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public ReportExportJobStatus Status { get; set; } = ReportExportJobStatus.Queued;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? LastPublishedAtUtc { get; set; }
    public int Attempts { get; set; }
    public byte[]? Content { get; set; }
    public int? RowCount { get; set; }
    public long? ContentLengthBytes { get; set; }
    public string? Error { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ReportingFilter ToFilter() => new(
        FromDate ?? (FromUtc.HasValue ? DateOnly.FromDateTime(FromUtc.Value.UtcDateTime) : null),
        ToDate ?? (ToUtc.HasValue ? DateOnly.FromDateTime(ToUtc.Value.UtcDateTime) : null),
        BranchId, CustomerId, Page, PageSize);

    public ReportExportJobResponse ToResponse() => new(
        Id, Report, Status.ToString(), CreatedAtUtc, StartedAtUtc, CompletedAtUtc,
        LastHeartbeatAtUtc, Attempts, RowCount, ContentLengthBytes,
        Status == ReportExportJobStatus.Completed ? $"/api/v1/reports/export-jobs/{Id:D}/download" : null,
        Error);
}

public enum ReportExportJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
}
