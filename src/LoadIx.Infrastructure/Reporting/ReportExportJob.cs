using LoadIx.Application.Reporting;
using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Reporting;

public sealed class ReportExportJob
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Report { get; set; } = string.Empty;
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? CustomerId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string Status { get; set; } = "Queued";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? LastPublishedAtUtc { get; set; }
    public int Attempts { get; set; }
    public byte[]? Content { get; set; }
    public string? Error { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ReportingFilter ToFilter() => new(FromUtc, ToUtc, BranchId, CustomerId, Page, PageSize);

    public ReportExportJobResponse ToResponse() => new(
        Id, Report, Status, CreatedAtUtc, CompletedAtUtc,
        Status == "Completed" ? $"/api/v1/reports/export-jobs/{Id:D}/download" : null,
        Error);
}
