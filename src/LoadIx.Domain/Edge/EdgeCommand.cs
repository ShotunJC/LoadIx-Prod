using LoadIx.Domain.Common;

namespace LoadIx.Domain.Edge;

public sealed class EdgeCommand : Entity<Guid>, ITenantOwned
{
    private EdgeCommand(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid credentialId,
        string commandType,
        string idempotencyKey,
        string nonce,
        string requestHash,
        DateTimeOffset deviceTimestampUtc,
        DateTimeOffset receivedAtUtc,
        string? correlationId)
        : base(id)
    {
        TenantId = tenantId;
        EdgeDeviceId = edgeDeviceId;
        CredentialId = credentialId;
        CommandType = commandType;
        IdempotencyKey = idempotencyKey;
        Nonce = nonce;
        RequestHash = requestHash;
        DeviceTimestampUtc = deviceTimestampUtc.ToUniversalTime();
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
        CorrelationId = DomainValidation.Optional(correlationId);
        Status = EdgeCommandStatus.Accepted;
    }

    private EdgeCommand()
    {
        CommandType = string.Empty;
        IdempotencyKey = string.Empty;
        Nonce = string.Empty;
        RequestHash = string.Empty;
        Status = EdgeCommandStatus.Accepted;
    }

    public TenantId TenantId { get; private set; }

    public Guid EdgeDeviceId { get; private set; }

    public Guid CredentialId { get; private set; }

    public string CommandType { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string Nonce { get; private set; }

    public string RequestHash { get; private set; }

    public DateTimeOffset DeviceTimestampUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public string? CorrelationId { get; private set; }

    public EdgeCommandStatus Status { get; private set; }

    public Guid? ResourceId { get; private set; }

    public string? FailureCode { get; private set; }

    public static Result<EdgeCommand> Create(
        Guid id,
        TenantId tenantId,
        Guid edgeDeviceId,
        Guid credentialId,
        string commandType,
        string idempotencyKey,
        string nonce,
        string requestHash,
        DateTimeOffset deviceTimestampUtc,
        DateTimeOffset receivedAtUtc,
        string? correlationId)
    {
        var error = DomainValidation.Required(commandType, "edge_command.type", 80)
            ?? DomainValidation.Required(
                idempotencyKey,
                "edge_command.idempotency_key",
                128)
            ?? DomainValidation.Required(nonce, "edge_command.nonce", 128)
            ?? DomainValidation.Required(
                requestHash,
                "edge_command.request_hash",
                64);
        if (id == Guid.Empty
            || tenantId.IsEmpty
            || edgeDeviceId == Guid.Empty
            || credentialId == Guid.Empty)
        {
            error = new DomainError(
                "edge_command.identifiers_required",
                "Command, tenant, device, and credential identifiers are required.");
        }

        return error is not null
            ? Result<EdgeCommand>.Failure(error)
            : Result<EdgeCommand>.Success(new EdgeCommand(
                id,
                tenantId,
                edgeDeviceId,
                credentialId,
                commandType.Trim(),
                idempotencyKey.Trim(),
                nonce.Trim(),
                requestHash.Trim(),
                deviceTimestampUtc,
                receivedAtUtc,
                correlationId));
    }

    public void Succeed(Guid? resourceId = null)
    {
        Status = EdgeCommandStatus.Succeeded;
        ResourceId = resourceId;
        FailureCode = null;
    }

    public void Fail(string failureCode)
    {
        Status = EdgeCommandStatus.Failed;
        FailureCode = failureCode;
    }
}
