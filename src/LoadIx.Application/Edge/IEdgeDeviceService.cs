namespace LoadIx.Application.Edge;

public interface IEdgeDeviceService
{
    Task<EdgeResult<EdgeDeviceListResponse>> ListAsync(
        EdgeDeviceQuery query,
        CancellationToken cancellationToken);

    Task<EdgeResult<IReadOnlyList<EdgeSiteOption>>> ListSitesAsync(
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeRegistrationBootstrap>> CreateRegistrationBootstrapAsync(
        EdgeRegistrationBootstrapModel model,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeCredentialIssued>> RegisterAsync(
        EdgeRegistrationModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeCredentialIssued>> RotateCredentialAsync(
        Guid? deviceId,
        EdgeRequestEnvelope? envelope,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeActionResponse>> RevokeAsync(
        Guid deviceId,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeDeviceDiagnostics>> GetDiagnosticsAsync(
        Guid deviceId,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeLiveReading>> GetLiveReadingAsync(
        Guid deviceId,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeConfiguration>> GetConfigurationAsync(
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeHeartbeatAck>> SubmitHeartbeatAsync(
        EdgeHeartbeatModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeReadingReceipt>> SubmitReadingAsync(
        EdgeReadingModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeTicketReceipt>> SubmitTicketAsync(
        EdgeTicketModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken);

    Task<EdgeResult<EdgeDiagnosticStatus>> GetDiagnosticStatusAsync(
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken);
}
