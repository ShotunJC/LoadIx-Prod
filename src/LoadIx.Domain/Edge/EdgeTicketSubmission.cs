using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class EdgeTicketSubmission : Entity<Guid>, ITenantOwned
{
    private EdgeTicketSubmission(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid weighbridgeDeviceId,
        Guid edgeCommandId,
        string ticketNumber,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc,
        string content,
        string? contentType,
        string? metadataJson,
        string correlationId)
        : base(id)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        WeighbridgeDeviceId = weighbridgeDeviceId;
        EdgeCommandId = edgeCommandId;
        TicketNumber = ticketNumber;
        CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
        Content = content;
        ContentType = DomainValidation.Optional(contentType);
        MetadataJson = DomainValidation.Optional(metadataJson);
        CorrelationId = correlationId;
    }

    private EdgeTicketSubmission()
    {
        TicketNumber = string.Empty;
        Content = string.Empty;
        CorrelationId = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public Guid WeighbridgeDeviceId { get; private set; }

    public Guid EdgeCommandId { get; private set; }

    public string TicketNumber { get; private set; }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public string Content { get; private set; }

    public string? ContentType { get; private set; }

    public string? MetadataJson { get; private set; }

    public string CorrelationId { get; private set; }

    public static Result<EdgeTicketSubmission> Create(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid weighbridgeDeviceId,
        Guid edgeCommandId,
        string ticketNumber,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset receivedAtUtc,
        string content,
        string? contentType,
        string? metadataJson,
        string correlationId)
    {
        var error = DomainValidation.Required(
                ticketNumber,
                "ticket.ticket_number",
                120)
            ?? DomainValidation.Required(content, "ticket.content", 64_000)
            ?? DomainValidation.Required(
                correlationId,
                "ticket.correlation_id",
                128);
        if (id == Guid.Empty
            || tenantId.IsEmpty
            || edgeDeviceId == Guid.Empty
            || weighbridgeDeviceId == Guid.Empty
            || edgeCommandId == Guid.Empty)
        {
            error = new DomainError(
                "ticket.identifiers_required",
                "Ticket, tenant, device, weighbridge, and command identifiers are required.");
        }
        else if (contentType?.Trim().Length > 80)
        {
            error = new DomainError(
                "ticket.content_type_too_long",
                "Ticket content type cannot exceed 80 characters.");
        }
        else if (metadataJson?.Length > 4000)
        {
            error = new DomainError(
                "ticket.metadata_too_long",
                "Ticket metadata cannot exceed 4000 characters.");
        }

        return error is not null
            ? Result<EdgeTicketSubmission>.Failure(error)
            : Result<EdgeTicketSubmission>.Success(new EdgeTicketSubmission(
                id,
                tenantId,
                edgeDeviceId,
                weighbridgeDeviceId,
                edgeCommandId,
                ticketNumber.Trim(),
                capturedAtUtc,
                receivedAtUtc,
                content,
                contentType,
                metadataJson,
                correlationId.Trim()));
    }
}
