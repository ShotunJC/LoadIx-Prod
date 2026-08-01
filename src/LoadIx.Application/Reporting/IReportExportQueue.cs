using LoadIx.Domain.Common;

namespace LoadIx.Application.Reporting;

public interface IReportExportService
{
    Task<ReportExportJobResponse> EnqueueAsync(string report, ReportingFilter filter, TenantId tenantId, Guid? userId, CancellationToken cancellationToken);

    ReportExportJobResponse? GetJob(Guid jobId, TenantId tenantId);

    byte[]? GetContent(Guid jobId, TenantId tenantId);
}
