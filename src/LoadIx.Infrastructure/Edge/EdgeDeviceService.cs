using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Edge;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Domain.Edge;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using LoadIx.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoadIx.Infrastructure.Edge;

public sealed class EdgeDeviceService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    IEdgeDeviceRequestContext edgeRequestContext,
    CurrentEdgeDeviceRequestContext currentEdgeRequestContext,
    CurrentRequestContext trustedRequestContext,
    IPasswordHasher<EdgeDeviceCredential> passwordHasher,
    SecurityAuditWriter auditWriter,
    IOptions<EdgeDeviceOptions> options,
    TimeProvider timeProvider) : IEdgeDeviceService
{
    private const int MaximumPageSize = 100;

    public async Task<EdgeResult<EdgeDeviceListResponse>> ListAsync(
        EdgeDeviceQuery query,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<EdgeDeviceListResponse>(
            LoadIxPermissions.EdgeDeviceView);
        if (denied is not null) return denied;
        if (query.Page < 1
            || query.PageSize is < 1 or > MaximumPageSize
            || query.Direction is not ("asc" or "desc"))
        {
            return Fail<EdgeDeviceListResponse>(
                EdgeFailure.Validation,
                "edge_devices.query_invalid",
                "Page must be positive, pageSize must be between 1 and 100, and direction must be asc or desc.");
        }

        var devices = dbContext.EdgeDevices.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            devices = devices.Where(device => device.Name.Contains(search));
        }

        var total = await devices.CountAsync(cancellationToken);
        var descending = query.Direction == "desc";
        devices = query.Sort switch
        {
            "name" => descending
                ? devices.OrderByDescending(device => device.Name)
                    .ThenByDescending(device => device.Id)
                : devices.OrderBy(device => device.Name)
                    .ThenBy(device => device.Id),
            "heartbeat" => descending
                ? devices.OrderByDescending(device => device.LastHeartbeatAtUtc)
                    .ThenByDescending(device => device.Id)
                : devices.OrderBy(device => device.LastHeartbeatAtUtc)
                    .ThenBy(device => device.Id),
            "status" => descending
                ? devices.OrderByDescending(device => device.Status)
                    .ThenByDescending(device => device.Id)
                : devices.OrderBy(device => device.Status)
                    .ThenBy(device => device.Id),
            _ => null!,
        };
        if (devices is null)
        {
            return Fail<EdgeDeviceListResponse>(
                EdgeFailure.Validation,
                "edge_devices.sort_invalid",
                "The requested edge device sort field is invalid.");
        }

        var rows = await devices
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var siteIds = rows.Select(device => device.SiteId).Distinct().ToArray();
        var sites = await dbContext.Locations.AsNoTracking()
            .Where(site => siteIds.Contains(site.Id))
            .ToDictionaryAsync(site => site.Id, cancellationToken);
        var deviceIds = rows.Select(device => device.Id).ToArray();
        var credentials = await dbContext.EdgeDeviceCredentials.AsNoTracking()
            .Where(credential => deviceIds.Contains(credential.EdgeDeviceId)
                && credential.Type == EdgeDeviceCredentialType.Device
                && credential.RevokedAtUtc == null)
            .GroupBy(credential => credential.EdgeDeviceId)
            .Select(group => group.OrderByDescending(item => item.CreatedAtUtc).First())
            .ToDictionaryAsync(credential => credential.EdgeDeviceId, cancellationToken);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var offlineAt = now.AddSeconds(-options.Value.OfflineAfterSeconds);
        var items = rows.Select(device =>
        {
            sites.TryGetValue(device.SiteId, out var site);
            credentials.TryGetValue(device.Id, out var credential);
            return new EdgeDeviceListItem(
                device.Id,
                device.Name,
                device.Status.ToString(),
                device.SiteId,
                site?.Name ?? "Unknown site",
                site?.Code ?? string.Empty,
                device.Protocol,
                device.ParserVersion,
                device.Status == EdgeDeviceStatus.Active
                    && device.LastHeartbeatAtUtc >= offlineAt,
                device.LastHeartbeatAtUtc,
                device.LastAgentVersion,
                device.LastHeartbeatStatus,
                credential?.Id,
                credential?.CreatedAtUtc,
                Version(device.RowVersion));
        }).ToArray();
        return EdgeResult<EdgeDeviceListResponse>.Success(new EdgeDeviceListResponse(
            items,
            query.Page,
            query.PageSize,
            total,
            (int)Math.Ceiling(total / (double)query.PageSize)));
    }

    public async Task<EdgeResult<IReadOnlyList<EdgeSiteOption>>> ListSitesAsync(
        CancellationToken cancellationToken)
    {
        var denied = Authorize<IReadOnlyList<EdgeSiteOption>>(
            LoadIxPermissions.EdgeDeviceManage);
        if (denied is not null) return denied;

        var sites = await dbContext.Locations.AsNoTracking()
            .Where(site => site.IsActive)
            .OrderBy(site => site.Name)
            .ThenBy(site => site.Id)
            .Take(500)
            .Select(site => new EdgeSiteOption(site.Id, site.Name, site.Code))
            .ToListAsync(cancellationToken);
        return EdgeResult<IReadOnlyList<EdgeSiteOption>>.Success(sites);
    }

    public async Task<EdgeResult<EdgeRegistrationBootstrap>>
        CreateRegistrationBootstrapAsync(
            EdgeRegistrationBootstrapModel model,
            CancellationToken cancellationToken)
    {
        var denied = Authorize<EdgeRegistrationBootstrap>(
            LoadIxPermissions.EdgeDeviceManage);
        if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(model.Name)
            || string.IsNullOrWhiteSpace(model.WeighbridgeName)
            || string.IsNullOrWhiteSpace(model.WeighbridgeCode)
            || string.IsNullOrWhiteSpace(model.Protocol)
            || string.IsNullOrWhiteSpace(model.ParserVersion)
            || model.SiteId == Guid.Empty)
        {
            return Fail<EdgeRegistrationBootstrap>(
                EdgeFailure.Validation,
                "edge_devices.bootstrap_invalid",
                "A device name, site, weighbridge identity, protocol, and parser version are required.");
        }

        var tenantId = requestContext.TenantId!.Value;
        var site = await dbContext.Locations.AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == model.SiteId && candidate.IsActive,
                cancellationToken);
        if (site is null)
        {
            return Fail<EdgeRegistrationBootstrap>(
                EdgeFailure.NotFound,
                "edge_devices.site_not_found",
                "The assigned site was not found or is inactive.");
        }

        if (await dbContext.EdgeDevices.AnyAsync(
                device => device.Name == model.Name.Trim(),
                cancellationToken))
        {
            return Fail<EdgeRegistrationBootstrap>(
                EdgeFailure.Conflict,
                "edge_devices.name_exists",
                "An edge device with this name already exists.");
        }

        var normalizedWeighbridgeCode = model.WeighbridgeCode.Trim().ToUpperInvariant();
        if (await dbContext.WeighbridgeDevices.AnyAsync(
                device => device.Code == normalizedWeighbridgeCode,
                cancellationToken))
        {
            return Fail<EdgeRegistrationBootstrap>(
                EdgeFailure.Conflict,
                "edge_devices.weighbridge_code_exists",
                "A weighbridge device with this code already exists.");
        }

        var deviceId = Guid.NewGuid();
        var weighbridgeId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var deviceResult = EdgeDevice.Create(
            deviceId,
            tenantId,
            model.Name,
            model.SiteId,
            model.Protocol,
            model.ParserVersion);
        if (deviceResult.IsFailure)
            return Validation<EdgeRegistrationBootstrap>(deviceResult.Error!);

        var weighbridgeResult = WeighbridgeDevice.Create(
            weighbridgeId,
            tenantId,
            deviceId,
            model.SiteId,
            model.WeighbridgeName,
            model.WeighbridgeCode,
            model.Protocol,
            model.ParserVersion);
        if (weighbridgeResult.IsFailure)
            return Validation<EdgeRegistrationBootstrap>(weighbridgeResult.Error!);

        var bootstrapToken = EdgeCredentialSecretGenerator.Create();
        var bootstrapExpiry = now.AddMinutes(options.Value.BootstrapLifetimeMinutes);
        var credentialResult = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            tenantId,
            deviceId,
            EdgeDeviceCredentialType.Bootstrap,
            passwordHasher.HashPassword(null!, bootstrapToken),
            now,
            bootstrapExpiry);
        if (credentialResult.IsFailure)
            return Validation<EdgeRegistrationBootstrap>(credentialResult.Error!);

        dbContext.EdgeDevices.Add(deviceResult.Value);
        dbContext.WeighbridgeDevices.Add(weighbridgeResult.Value);
        dbContext.EdgeDeviceCredentials.Add(credentialResult.Value);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicate(exception))
        {
            return Fail<EdgeRegistrationBootstrap>(
                EdgeFailure.Conflict,
                "edge_devices.duplicate",
                "The edge device or weighbridge code already exists.");
        }

        await auditWriter.WriteAsync(
            "EdgeDevice.RegistrationBootstrapCreated",
            "Succeeded",
            "EdgeDevice",
            deviceId.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken);
        return EdgeResult<EdgeRegistrationBootstrap>.Success(
            new EdgeRegistrationBootstrap(
                deviceId,
                weighbridgeId,
                deviceResult.Value.Name,
                site.Name,
                bootstrapToken,
                bootstrapExpiry));
    }

    public async Task<EdgeResult<EdgeCredentialIssued>> RegisterAsync(
        EdgeRegistrationModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var envelopeFailure = ValidateEnvelope<EdgeCredentialIssued>(envelope);
        if (envelopeFailure is not null)
            return envelopeFailure;
        if (model.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(model.BootstrapToken))
        {
            return Fail<EdgeCredentialIssued>(
                EdgeFailure.Validation,
                "edge_devices.registration_invalid",
                "A device identifier and one-time bootstrap token are required.");
        }

        var device = await dbContext.EdgeDevices.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == model.DeviceId, cancellationToken);
        var bootstrap = await dbContext.EdgeDeviceCredentials.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.EdgeDeviceId == model.DeviceId
                && item.Type == EdgeDeviceCredentialType.Bootstrap
                && item.RevokedAtUtc == null, cancellationToken);
        if (device is null
            || bootstrap is null
            || device.Status != EdgeDeviceStatus.PendingRegistration
            || !bootstrap.IsUsableAt(timeProvider.GetUtcNow())
            || passwordHasher.VerifyHashedPassword(
                null!,
                bootstrap.SecretHash,
                model.BootstrapToken)
                == PasswordVerificationResult.Failed)
        {
            return Fail<EdgeCredentialIssued>(
                EdgeFailure.Forbidden,
                "edge_devices.bootstrap_invalid",
                "The registration bootstrap is invalid or expired.");
        }

        trustedRequestContext.Set(
            device.TenantId,
            null,
            false,
            [LoadIxPermissions.EdgeDeviceReceive]);
        currentEdgeRequestContext.Set(
            device.TenantId,
            device.Id,
            bootstrap.Id,
            envelope.Nonce,
            envelope.IdempotencyKey,
            envelope.TimestampUtc,
            envelope.ClientIpAddress);
        var existing = await dbContext.EdgeCommands.IgnoreQueryFilters()
            .SingleOrDefaultAsync(command => command.EdgeDeviceId == device.Id
                && (command.Nonce == envelope.Nonce
                    || command.IdempotencyKey == envelope.IdempotencyKey),
                cancellationToken);
        if (existing is not null)
        {
            return Fail<EdgeCredentialIssued>(
                EdgeFailure.Conflict,
                "edge_devices.bootstrap_replay",
                "A registration bootstrap request can only be used once.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var registerResult = device.Register(now, model.AgentVersion);
        if (registerResult.IsFailure)
            return Validation<EdgeCredentialIssued>(registerResult.Error!);
        bootstrap.Revoke(now);
        var secret = EdgeCredentialSecretGenerator.Create();
        var credentialResult = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            device.TenantId,
            device.Id,
            EdgeDeviceCredentialType.Device,
            passwordHasher.HashPassword(null!, secret),
            now);
        if (credentialResult.IsFailure)
            return Validation<EdgeCredentialIssued>(credentialResult.Error!);

        var commandResult = EdgeCommand.Create(
            Guid.NewGuid(),
            device.TenantId,
            device.Id,
            bootstrap.Id,
            "Registration",
            envelope.IdempotencyKey,
            envelope.Nonce,
            Hash(model.DeviceId, model.AgentVersion ?? string.Empty),
            envelope.TimestampUtc,
            now,
            null);
        if (commandResult.IsFailure)
            return Validation<EdgeCredentialIssued>(commandResult.Error!);
        commandResult.Value.Succeed(credentialResult.Value.Id);
        dbContext.EdgeDeviceCredentials.Add(credentialResult.Value);
        dbContext.EdgeCommands.Add(commandResult.Value);
        AddEdgeAudit(
            "EdgeDevice.Registered",
            device.Id,
            null,
            envelope.ClientIpAddress,
            now,
            null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return EdgeResult<EdgeCredentialIssued>.Success(new EdgeCredentialIssued(
            device.Id,
            credentialResult.Value.Id,
            credentialResult.Value.Type.ToString(),
            secret,
            null));
    }

    public async Task<EdgeResult<EdgeCredentialIssued>> RotateCredentialAsync(
        Guid? deviceId,
        EdgeRequestEnvelope? envelope,
        CancellationToken cancellationToken)
    {
        var isDeviceRequest = envelope is not null;
        if (isDeviceRequest)
        {
            var contextFailure = await LoadDeviceAsync(envelope!, cancellationToken);
            if (contextFailure.Failure != EdgeFailure.None)
                return Fail<EdgeCredentialIssued>(
                    contextFailure.Failure,
                    contextFailure.Code,
                    contextFailure.Message);
            deviceId = edgeRequestContext.EdgeDeviceId;
            var start = await BeginCommandAsync(
                "CredentialRotation",
                Hash(deviceId!.Value),
                null,
                envelope!,
                cancellationToken);
            if (start.Failure != EdgeFailure.None)
                return Fail<EdgeCredentialIssued>(
                    start.Failure,
                    start.Code,
                    start.Message);
            if (start.IsReplay)
            {
                return Fail<EdgeCredentialIssued>(
                    EdgeFailure.Conflict,
                    "edge_devices.rotation_replay",
                    "The new credential is returned only once; start a new rotation if it was not stored by the agent.");
            }

            var result = await RotateCredentialCoreAsync(
                deviceId!.Value,
                envelope!,
                start.Command!,
                cancellationToken);
            return result;
        }

        var denied = Authorize<EdgeCredentialIssued>(LoadIxPermissions.EdgeDeviceManage);
        if (denied is not null) return denied;
        if (!deviceId.HasValue || deviceId.Value == Guid.Empty)
        {
            return Fail<EdgeCredentialIssued>(
                EdgeFailure.Validation,
                "edge_devices.identifier_required",
                "An edge device identifier is required.");
        }

        var device = await dbContext.EdgeDevices
            .SingleOrDefaultAsync(item => item.Id == deviceId.Value, cancellationToken);
        if (device is null) return NotFound<EdgeCredentialIssued>();
        var active = await dbContext.EdgeDeviceCredentials
            .Where(item => item.EdgeDeviceId == device.Id
                && item.Type == EdgeDeviceCredentialType.Device
                && item.RevokedAtUtc == null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (active is null)
        {
            return Fail<EdgeCredentialIssued>(
                EdgeFailure.Conflict,
                "edge_devices.credential_missing",
                "The edge device has no active credential to rotate.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        active.Revoke(now);
        var secret = EdgeCredentialSecretGenerator.Create();
        var credentialResult = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            device.TenantId,
            device.Id,
            EdgeDeviceCredentialType.Device,
            passwordHasher.HashPassword(null!, secret),
            now,
            null,
            active.Id);
        if (credentialResult.IsFailure)
            return Validation<EdgeCredentialIssued>(credentialResult.Error!);
        dbContext.EdgeDeviceCredentials.Add(credentialResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "EdgeDevice.CredentialRotated",
            "Succeeded",
            "EdgeDevice",
            device.Id.ToString(),
            device.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken);
        return EdgeResult<EdgeCredentialIssued>.Success(new EdgeCredentialIssued(
            device.Id,
            credentialResult.Value.Id,
            credentialResult.Value.Type.ToString(),
            secret,
            null));
    }

    public async Task<EdgeResult<EdgeActionResponse>> RevokeAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<EdgeActionResponse>(LoadIxPermissions.EdgeDeviceManage);
        if (denied is not null) return denied;
        var device = await dbContext.EdgeDevices
            .SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        if (device is null) return NotFound<EdgeActionResponse>();
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        device.Revoke(now);
        var credentials = await dbContext.EdgeDeviceCredentials
            .Where(item => item.EdgeDeviceId == device.Id && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var credential in credentials) credential.Revoke(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "EdgeDevice.Revoked",
            "Succeeded",
            "EdgeDevice",
            device.Id.ToString(),
            device.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken);
        return EdgeResult<EdgeActionResponse>.Success(
            new EdgeActionResponse(device.Id, device.Status.ToString()));
    }

    public async Task<EdgeResult<EdgeDeviceDiagnostics>> GetDiagnosticsAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<EdgeDeviceDiagnostics>(LoadIxPermissions.EdgeDeviceView);
        if (denied is not null) return denied;
        var device = await dbContext.EdgeDevices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        if (device is null) return NotFound<EdgeDeviceDiagnostics>();
        var heartbeats = await dbContext.DeviceHeartbeats.AsNoTracking()
            .Where(item => item.EdgeDeviceId == deviceId)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(50)
            .Select(item => new EdgeHeartbeatItem(
                item.Id,
                item.DeviceTimestampUtc,
                item.ReceivedAtUtc,
                item.AgentVersion,
                item.Status,
                item.MetadataJson))
            .ToListAsync(cancellationToken);
        var commands = await dbContext.EdgeCommands.AsNoTracking()
            .Where(item => item.EdgeDeviceId == deviceId)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(50)
            .Select(item => new EdgeCommandItem(
                item.Id,
                item.CommandType,
                item.Status.ToString(),
                item.IdempotencyKey,
                item.Nonce,
                item.CorrelationId,
                item.DeviceTimestampUtc,
                item.ReceivedAtUtc,
                item.ResourceId,
                item.FailureCode))
            .ToListAsync(cancellationToken);
        var readings = await dbContext.RawWeightReadings.AsNoTracking()
            .Where(item => item.EdgeDeviceId == deviceId)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(50)
            .Select(item => new EdgeReadingItem(
                item.Id,
                item.WeighbridgeDeviceId,
                item.DisplayedValue,
                item.Unit.ToString(),
                item.IsStable,
                item.CapturedAtUtc,
                item.ReceivedAtUtc,
                item.VehicleRegistration,
                item.ParserVersion,
                item.CorrelationId))
            .ToListAsync(cancellationToken);
        var auditEvents = await dbContext.SecurityAuditEvents.AsNoTracking()
            .Where(item => item.TenantId == device.TenantId
                && item.ActorDeviceId == deviceId)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(50)
            .Select(item => new EdgeAuditItem(
                item.Id,
                item.EventType,
                item.Outcome,
                item.TargetId,
                item.ActorDeviceId,
                item.OccurredAtUtc,
                item.CorrelationId))
            .ToListAsync(cancellationToken);
        return EdgeResult<EdgeDeviceDiagnostics>.Success(new EdgeDeviceDiagnostics(
            device.Id,
            device.Name,
            device.Status.ToString(),
            device.LastHeartbeatAtUtc,
            heartbeats,
            commands,
            readings,
            auditEvents));
    }

    public async Task<EdgeResult<EdgeLiveReading>> GetLiveReadingAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<EdgeLiveReading>(LoadIxPermissions.EdgeDeviceView);
        if (denied is not null) return denied;
        if (!await dbContext.EdgeDevices.AsNoTracking()
                .AnyAsync(item => item.Id == deviceId, cancellationToken))
        {
            return NotFound<EdgeLiveReading>();
        }

        var latest = await dbContext.RawWeightReadings.AsNoTracking()
            .Where(item => item.EdgeDeviceId == deviceId)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new EdgeReadingItem(
                item.Id,
                item.WeighbridgeDeviceId,
                item.DisplayedValue,
                item.Unit.ToString(),
                item.IsStable,
                item.CapturedAtUtc,
                item.ReceivedAtUtc,
                item.VehicleRegistration,
                item.ParserVersion,
                item.CorrelationId))
            .FirstOrDefaultAsync(cancellationToken);
        var stable = await dbContext.StableWeightReadings.AsNoTracking()
            .Where(item => item.EdgeDeviceId == deviceId)
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new EdgeReadingItem(
                item.RawWeightReadingId,
                item.WeighbridgeDeviceId,
                item.DisplayedValue,
                item.Unit.ToString(),
                true,
                item.CapturedAtUtc,
                item.ReceivedAtUtc,
                item.VehicleRegistration,
                string.Empty,
                item.CorrelationId))
            .FirstOrDefaultAsync(cancellationToken);
        return EdgeResult<EdgeLiveReading>.Success(new EdgeLiveReading(
            latest,
            stable,
            timeProvider.GetUtcNow().ToUniversalTime()));
    }

    public async Task<EdgeResult<EdgeConfiguration>> GetConfigurationAsync(
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var contextFailure = await LoadDeviceAsync(envelope, cancellationToken);
        if (contextFailure.Failure != EdgeFailure.None)
            return Fail<EdgeConfiguration>(
                contextFailure.Failure,
                contextFailure.Code,
                contextFailure.Message);
        var device = contextFailure.Device!;
        var hash = Hash(device.Id, "configuration");
        var start = await BeginCommandAsync(
            "ConfigurationRetrieval",
            hash,
            null,
            envelope,
            cancellationToken);
        if (start.Failure != EdgeFailure.None)
            return Fail<EdgeConfiguration>(start.Failure, start.Code, start.Message);

        var configuration = await BuildConfigurationAsync(device, cancellationToken);
        if (!start.IsReplay)
        {
            start.Command!.Succeed();
            AddEdgeAudit(
                "EdgeDevice.ConfigurationRetrieved",
                device.Id,
                null,
                envelope.ClientIpAddress,
                timeProvider.GetUtcNow().ToUniversalTime(),
                null);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return EdgeResult<EdgeConfiguration>.Success(configuration);
    }

    public async Task<EdgeResult<EdgeHeartbeatAck>> SubmitHeartbeatAsync(
        EdgeHeartbeatModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var contextFailure = await LoadDeviceAsync(envelope, cancellationToken);
        if (contextFailure.Failure != EdgeFailure.None)
            return Fail<EdgeHeartbeatAck>(
                contextFailure.Failure,
                contextFailure.Code,
                contextFailure.Message);
        if (model.Metadata?.Count > 20)
        {
            return Fail<EdgeHeartbeatAck>(
                EdgeFailure.Validation,
                "edge.heartbeat.metadata_too_many",
                "A heartbeat may contain at most 20 metadata entries.");
        }

        var device = contextFailure.Device!;
        var metadata = model.Metadata is null
            ? null
            : JsonSerializer.Serialize(model.Metadata);
        var start = await BeginCommandAsync(
            "Heartbeat",
            Hash(model.DeviceTimestampUtc, model.AgentVersion ?? string.Empty,
                model.Status ?? string.Empty, metadata ?? string.Empty),
            null,
            envelope,
            cancellationToken);
        if (start.Failure != EdgeFailure.None)
            return Fail<EdgeHeartbeatAck>(start.Failure, start.Code, start.Message);
        if (start.IsReplay)
        {
            return EdgeResult<EdgeHeartbeatAck>.Success(new EdgeHeartbeatAck(
                start.Existing!.Id,
                start.Existing.ReceivedAtUtc,
                true));
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var heartbeatResult = DeviceHeartbeat.Create(
            Guid.NewGuid(),
            device.TenantId,
            device.Id,
            model.DeviceTimestampUtc,
            now,
            model.AgentVersion,
            model.Status,
            metadata);
        if (heartbeatResult.IsFailure)
            return Validation<EdgeHeartbeatAck>(heartbeatResult.Error!);
        var deviceResult = device.RecordHeartbeat(now, model.AgentVersion, model.Status);
        if (deviceResult.IsFailure)
            return Validation<EdgeHeartbeatAck>(deviceResult.Error!);
        start.Command!.Succeed();
        dbContext.DeviceHeartbeats.Add(heartbeatResult.Value);
        AddEdgeAudit(
            "EdgeDevice.HeartbeatReceived",
            device.Id,
            null,
            envelope.ClientIpAddress,
            now,
            null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return EdgeResult<EdgeHeartbeatAck>.Success(new EdgeHeartbeatAck(
            start.Command.Id,
            now,
            false));
    }

    public async Task<EdgeResult<EdgeReadingReceipt>> SubmitReadingAsync(
        EdgeReadingModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var contextFailure = await LoadDeviceAsync(envelope, cancellationToken);
        if (contextFailure.Failure != EdgeFailure.None)
            return Fail<EdgeReadingReceipt>(
                contextFailure.Failure,
                contextFailure.Code,
                contextFailure.Message);
        if (model.WeighbridgeDeviceId == Guid.Empty
            || string.IsNullOrWhiteSpace(model.SerialMessageMetadata)
            || string.IsNullOrWhiteSpace(model.ParserVersion))
        {
            return Fail<EdgeReadingReceipt>(
                EdgeFailure.Validation,
                "edge.reading.invalid",
                "A weighbridge, serial message metadata, and parser version are required.");
        }

        var device = contextFailure.Device!;
        var requestedCorrelationId = model.CorrelationId?.Trim() ?? string.Empty;
        var correlationId = string.IsNullOrWhiteSpace(requestedCorrelationId)
            ? Guid.NewGuid().ToString("N")
            : requestedCorrelationId;
        var start = await BeginCommandAsync(
            "WeightReading",
            Hash(
                model.WeighbridgeDeviceId,
                model.DisplayedValue,
                model.Unit,
                model.IsStable,
                model.CapturedAtUtc,
                model.VehicleRegistration ?? string.Empty,
                model.SerialMessageMetadata,
                model.ParserVersion,
                requestedCorrelationId),
            correlationId,
            envelope,
            cancellationToken);
        if (start.Failure != EdgeFailure.None)
            return Fail<EdgeReadingReceipt>(start.Failure, start.Code, start.Message);
        if (start.IsReplay)
        {
            if (!start.Existing!.ResourceId.HasValue)
                return Fail<EdgeReadingReceipt>(
                    EdgeFailure.Conflict,
                    "edge.reading.replay_missing",
                    "The prior reading submission has no stored result.");
            var replay = await MapReadingAsync(
                start.Existing.ResourceId.Value,
                start.Existing.Id,
                true,
                cancellationToken);
            return replay ?? NotFound<EdgeReadingReceipt>();
        }

        var weighbridge = await dbContext.WeighbridgeDevices
            .SingleOrDefaultAsync(item => item.Id == model.WeighbridgeDeviceId
                && item.EdgeDeviceId == device.Id
                && item.SiteId == device.SiteId
                && item.IsActive, cancellationToken);
        if (weighbridge is null)
        {
            return Fail<EdgeReadingReceipt>(
                EdgeFailure.Forbidden,
                "edge.reading.weighbridge_not_assigned",
                "The weighbridge is not assigned to this device and site.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var readingResult = RawWeightReading.Create(
            Guid.NewGuid(),
            device.TenantId,
            device.Id,
            weighbridge.Id,
            model.DisplayedValue,
            model.Unit,
            model.IsStable,
            model.CapturedAtUtc,
            now,
            model.VehicleRegistration,
            model.SerialMessageMetadata,
            model.ParserVersion,
            correlationId,
            start.Command!.Id);
        if (readingResult.IsFailure)
            return Validation<EdgeReadingReceipt>(readingResult.Error!);
        StableWeightReading? stable = null;
        if (model.IsStable)
        {
            var stableResult = StableWeightReading.Create(
                Guid.NewGuid(),
                device.TenantId,
                readingResult.Value.Id,
                device.Id,
                weighbridge.Id,
                model.DisplayedValue,
                model.Unit,
                model.CapturedAtUtc,
                now,
                model.VehicleRegistration,
                correlationId);
            if (stableResult.IsFailure)
                return Validation<EdgeReadingReceipt>(stableResult.Error!);
            stable = stableResult.Value;
            dbContext.StableWeightReadings.Add(stable);
        }

        start.Command!.Succeed(readingResult.Value.Id);
        dbContext.RawWeightReadings.Add(readingResult.Value);
        AddEdgeAudit(
            "EdgeDevice.WeightReadingReceived",
            device.Id,
            readingResult.Value.Id,
            envelope.ClientIpAddress,
            now,
            correlationId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return EdgeResult<EdgeReadingReceipt>.Success(new EdgeReadingReceipt(
            start.Command.Id,
            readingResult.Value.Id,
            stable?.Id,
            weighbridge.Id,
            readingResult.Value.DisplayedValue,
            readingResult.Value.Unit.ToString(),
            readingResult.Value.IsStable,
            readingResult.Value.CapturedAtUtc,
            readingResult.Value.ReceivedAtUtc,
            readingResult.Value.VehicleRegistration,
            readingResult.Value.ParserVersion,
            readingResult.Value.CorrelationId,
            false));
    }

    public async Task<EdgeResult<EdgeTicketReceipt>> SubmitTicketAsync(
        EdgeTicketModel model,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var contextFailure = await LoadDeviceAsync(envelope, cancellationToken);
        if (contextFailure.Failure != EdgeFailure.None)
            return Fail<EdgeTicketReceipt>(
                contextFailure.Failure,
                contextFailure.Code,
                contextFailure.Message);
        if (model.WeighbridgeDeviceId == Guid.Empty
            || string.IsNullOrWhiteSpace(model.TicketNumber)
            || string.IsNullOrWhiteSpace(model.Content)
            || model.Content.Length > options.Value.MaximumTicketCharacters)
        {
            return Fail<EdgeTicketReceipt>(
                EdgeFailure.Validation,
                "edge.ticket.invalid",
                "A weighbridge, ticket number, and bounded ticket content are required.");
        }
        if (model.Metadata?.Count > 20)
        {
            return Fail<EdgeTicketReceipt>(
                EdgeFailure.Validation,
                "edge.ticket.metadata_too_many",
                "A ticket may contain at most 20 metadata entries.");
        }

        var device = contextFailure.Device!;
        var requestedCorrelationId = model.CorrelationId?.Trim() ?? string.Empty;
        var correlationId = string.IsNullOrWhiteSpace(requestedCorrelationId)
            ? Guid.NewGuid().ToString("N")
            : requestedCorrelationId;
        var metadata = model.Metadata is null
            ? null
            : JsonSerializer.Serialize(model.Metadata);
        var start = await BeginCommandAsync(
            "TicketSubmission",
            Hash(
                model.WeighbridgeDeviceId,
                model.TicketNumber,
                model.CapturedAtUtc,
                model.Content,
                model.ContentType ?? string.Empty,
                metadata ?? string.Empty,
                requestedCorrelationId),
            correlationId,
            envelope,
            cancellationToken);
        if (start.Failure != EdgeFailure.None)
            return Fail<EdgeTicketReceipt>(start.Failure, start.Code, start.Message);
        if (start.IsReplay)
        {
            if (!start.Existing!.ResourceId.HasValue)
                return Fail<EdgeTicketReceipt>(
                    EdgeFailure.Conflict,
                    "edge.ticket.replay_missing",
                    "The prior ticket submission has no stored result.");
            var prior = await dbContext.EdgeTicketSubmissions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == start.Existing.ResourceId.Value,
                    cancellationToken);
            return prior is null
                ? NotFound<EdgeTicketReceipt>()
                : EdgeResult<EdgeTicketReceipt>.Success(new EdgeTicketReceipt(
                    start.Existing.Id,
                    prior.Id,
                    prior.WeighbridgeDeviceId,
                    prior.TicketNumber,
                    prior.CapturedAtUtc,
                    prior.ReceivedAtUtc,
                    prior.CorrelationId,
                    true));
        }

        var weighbridge = await dbContext.WeighbridgeDevices
            .SingleOrDefaultAsync(item => item.Id == model.WeighbridgeDeviceId
                && item.EdgeDeviceId == device.Id
                && item.SiteId == device.SiteId
                && item.IsActive, cancellationToken);
        if (weighbridge is null)
        {
            return Fail<EdgeTicketReceipt>(
                EdgeFailure.Forbidden,
                "edge.ticket.weighbridge_not_assigned",
                "The weighbridge is not assigned to this device and site.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var ticketResult = EdgeTicketSubmission.Create(
            Guid.NewGuid(),
            device.TenantId,
            device.Id,
            weighbridge.Id,
            start.Command!.Id,
            model.TicketNumber,
            model.CapturedAtUtc,
            now,
            model.Content,
            model.ContentType,
            metadata,
            correlationId);
        if (ticketResult.IsFailure)
            return Validation<EdgeTicketReceipt>(ticketResult.Error!);
        start.Command!.Succeed(ticketResult.Value.Id);
        dbContext.EdgeTicketSubmissions.Add(ticketResult.Value);
        AddEdgeAudit(
            "EdgeDevice.TicketReceived",
            device.Id,
            ticketResult.Value.Id,
            envelope.ClientIpAddress,
            now,
            correlationId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return EdgeResult<EdgeTicketReceipt>.Success(new EdgeTicketReceipt(
            start.Command.Id,
            ticketResult.Value.Id,
            weighbridge.Id,
            ticketResult.Value.TicketNumber,
            ticketResult.Value.CapturedAtUtc,
            ticketResult.Value.ReceivedAtUtc,
            ticketResult.Value.CorrelationId,
            false));
    }

    public async Task<EdgeResult<EdgeDiagnosticStatus>> GetDiagnosticStatusAsync(
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var contextFailure = await LoadDeviceAsync(envelope, cancellationToken);
        if (contextFailure.Failure != EdgeFailure.None)
            return Fail<EdgeDiagnosticStatus>(
                contextFailure.Failure,
                contextFailure.Code,
                contextFailure.Message);
        var device = contextFailure.Device!;
        var start = await BeginCommandAsync(
            "DiagnosticStatus",
            Hash(device.Id, "diagnostic-status"),
            null,
            envelope,
            cancellationToken);
        if (start.Failure != EdgeFailure.None)
            return Fail<EdgeDiagnosticStatus>(start.Failure, start.Code, start.Message);
        var status = await BuildDiagnosticStatusAsync(device, cancellationToken);
        if (!start.IsReplay)
        {
            start.Command!.Succeed();
            AddEdgeAudit(
                "EdgeDevice.DiagnosticStatusRequested",
                device.Id,
                null,
                envelope.ClientIpAddress,
                timeProvider.GetUtcNow().ToUniversalTime(),
                null);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return EdgeResult<EdgeDiagnosticStatus>.Success(status);
    }

    private async Task<EdgeResult<EdgeCredentialIssued>> RotateCredentialCoreAsync(
        Guid deviceId,
        EdgeRequestEnvelope envelope,
        EdgeCommand command,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.EdgeDevices
            .SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        if (device is null) return NotFound<EdgeCredentialIssued>();
        var active = await dbContext.EdgeDeviceCredentials
            .Where(item => item.EdgeDeviceId == device.Id
                && item.Type == EdgeDeviceCredentialType.Device
                && item.RevokedAtUtc == null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (active is null)
        {
            return Fail<EdgeCredentialIssued>(
                EdgeFailure.Conflict,
                "edge_devices.credential_missing",
                "The edge device has no active credential to rotate.");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        active.Revoke(now);
        var secret = EdgeCredentialSecretGenerator.Create();
        var credentialResult = EdgeDeviceCredential.Create(
            Guid.NewGuid(),
            device.TenantId,
            device.Id,
            EdgeDeviceCredentialType.Device,
            passwordHasher.HashPassword(null!, secret),
            now,
            null,
            active.Id);
        if (credentialResult.IsFailure)
            return Validation<EdgeCredentialIssued>(credentialResult.Error!);
        command.Succeed(credentialResult.Value.Id);
        dbContext.EdgeDeviceCredentials.Add(credentialResult.Value);
        AddEdgeAudit(
            "EdgeDevice.CredentialRotated",
            device.Id,
            null,
            envelope.ClientIpAddress,
            now,
            null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return EdgeResult<EdgeCredentialIssued>.Success(new EdgeCredentialIssued(
            device.Id,
            credentialResult.Value.Id,
            credentialResult.Value.Type.ToString(),
            secret,
            null));
    }

    private async Task<EdgeContextLoad> LoadDeviceAsync(
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var envelopeFailure = ValidateEnvelope<object>(envelope);
        if (envelopeFailure is not null)
        {
            return EdgeContextLoad.Rejected(
                envelopeFailure.Failure,
                envelopeFailure.Code,
                envelopeFailure.Message);
        }

        if (edgeRequestContext.TenantId is not { } tenantId
            || edgeRequestContext.EdgeDeviceId is not { } deviceId
            || edgeRequestContext.CredentialId is not { } credentialId
            || !string.Equals(
                edgeRequestContext.Nonce,
                envelope.Nonce,
                StringComparison.Ordinal)
            || !string.Equals(
                edgeRequestContext.IdempotencyKey,
                envelope.IdempotencyKey,
                StringComparison.Ordinal))
        {
            return EdgeContextLoad.Rejected(
                EdgeFailure.Forbidden,
                "edge.authentication.required",
                "A valid device authentication context is required.");
        }

        var device = await dbContext.EdgeDevices.SingleOrDefaultAsync(
            item => item.Id == deviceId
                && item.TenantId == tenantId,
            cancellationToken);
        var credential = await dbContext.EdgeDeviceCredentials.SingleOrDefaultAsync(
            item => item.Id == credentialId
                && item.EdgeDeviceId == deviceId
                && item.Type == EdgeDeviceCredentialType.Device,
            cancellationToken);
        if (device is null
            || credential is null
            || device.Status != EdgeDeviceStatus.Active
            || !credential.IsUsableAt(timeProvider.GetUtcNow()))
        {
            return EdgeContextLoad.Rejected(
                EdgeFailure.Forbidden,
                "edge.authentication.revoked",
                "The edge device or credential is no longer active.");
        }

        credential.MarkUsed(timeProvider.GetUtcNow().ToUniversalTime());
        return EdgeContextLoad.Success(device, credential);
    }

    private async Task<CommandStart> BeginCommandAsync(
        string commandType,
        string requestHash,
        string? correlationId,
        EdgeRequestEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (edgeRequestContext.TenantId is not { } tenantId
            || edgeRequestContext.EdgeDeviceId is not { } deviceId
            || edgeRequestContext.CredentialId is not { } credentialId)
        {
            return CommandStart.Rejected(
                EdgeFailure.Forbidden,
                "edge.authentication.required",
                "A valid device authentication context is required.");
        }

        var existingByNonce = await dbContext.EdgeCommands.AsNoTracking()
            .SingleOrDefaultAsync(command => command.EdgeDeviceId == deviceId
                && command.Nonce == envelope.Nonce, cancellationToken);
        if (existingByNonce is not null)
        {
            return Matches(existingByNonce, commandType, requestHash, envelope)
                ? CommandStart.Replay(existingByNonce)
                : CommandStart.Rejected(
                    EdgeFailure.Conflict,
                    "edge.replay_detected",
                    "The request nonce has already been used with different content.");
        }

        var existingByKey = await dbContext.EdgeCommands.AsNoTracking()
            .SingleOrDefaultAsync(command => command.EdgeDeviceId == deviceId
                && command.IdempotencyKey == envelope.IdempotencyKey,
                cancellationToken);
        if (existingByKey is not null)
        {
            return Matches(existingByKey, commandType, requestHash, envelope)
                ? CommandStart.Replay(existingByKey)
                : CommandStart.Rejected(
                    EdgeFailure.Conflict,
                    "edge.idempotency_payload_mismatch",
                    "The idempotency key was already used for different content.");
        }

        var commandResult = EdgeCommand.Create(
            Guid.NewGuid(),
            tenantId,
            deviceId,
            credentialId,
            commandType,
            envelope.IdempotencyKey,
            envelope.Nonce,
            requestHash,
            envelope.TimestampUtc,
            timeProvider.GetUtcNow().ToUniversalTime(),
            correlationId);
        return commandResult.IsFailure
            ? CommandStart.Rejected(
                EdgeFailure.Validation,
                commandResult.Error!.Code,
                commandResult.Error.Description)
            : AddAndReturnCommand(commandResult.Value);
    }

    private async Task<EdgeConfiguration> BuildConfigurationAsync(
        EdgeDevice device,
        CancellationToken cancellationToken)
    {
        var site = await dbContext.Locations.AsNoTracking()
            .Where(item => item.Id == device.SiteId)
            .Select(item => new { item.Name, item.Code })
            .SingleAsync(cancellationToken);
        var weighbridges = await dbContext.WeighbridgeDevices.AsNoTracking()
            .Where(item => item.EdgeDeviceId == device.Id)
            .OrderBy(item => item.Code)
            .ThenBy(item => item.Id)
            .Select(item => new EdgeConfigurationWeighbridge(
                item.Id,
                item.Name,
                item.Code,
                item.SiteId,
                item.Protocol,
                item.ParserVersion,
                item.IsActive))
            .ToListAsync(cancellationToken);
        return new EdgeConfiguration(
            device.Id,
            device.Name,
            device.SiteId,
            site.Name,
            site.Code,
            device.Protocol,
            device.ParserVersion,
            options.Value.HeartbeatIntervalSeconds,
            timeProvider.GetUtcNow().ToUniversalTime(),
            weighbridges);
    }

    private async Task<EdgeDiagnosticStatus> BuildDiagnosticStatusAsync(
        EdgeDevice device,
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.EdgeCommands.CountAsync(
            item => item.EdgeDeviceId == device.Id
                && item.Status == EdgeCommandStatus.Accepted,
            cancellationToken);
        var readings = await dbContext.RawWeightReadings.CountAsync(
            item => item.EdgeDeviceId == device.Id,
            cancellationToken);
        var tickets = await dbContext.EdgeTicketSubmissions.CountAsync(
            item => item.EdgeDeviceId == device.Id,
            cancellationToken);
        return new EdgeDiagnosticStatus(
            device.Id,
            device.Status.ToString(),
            timeProvider.GetUtcNow().ToUniversalTime(),
            device.LastHeartbeatAtUtc,
            pending,
            readings,
            tickets);
    }

    private async Task<EdgeResult<EdgeReadingReceipt>?> MapReadingAsync(
        Guid readingId,
        Guid commandId,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var reading = await dbContext.RawWeightReadings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == readingId, cancellationToken);
        if (reading is null) return null;
        var stableId = await dbContext.StableWeightReadings.AsNoTracking()
            .Where(item => item.RawWeightReadingId == reading.Id)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return EdgeResult<EdgeReadingReceipt>.Success(new EdgeReadingReceipt(
            commandId,
            reading.Id,
            stableId,
            reading.WeighbridgeDeviceId,
            reading.DisplayedValue,
            reading.Unit.ToString(),
            reading.IsStable,
            reading.CapturedAtUtc,
            reading.ReceivedAtUtc,
            reading.VehicleRegistration,
            reading.ParserVersion,
            reading.CorrelationId,
            isReplay));
    }

    private EdgeResult<T>? Authorize<T>(string permission)
    {
        if (!requestContext.TenantId.HasValue
            || !requestContext.UserId.HasValue
            || !requestContext.HasPermission(permission))
        {
            return Fail<T>(
                EdgeFailure.Forbidden,
                "authorization.denied",
                "You do not have permission to perform this operation.");
        }

        return null;
    }

    private EdgeResult<T>? ValidateEnvelope<T>(EdgeRequestEnvelope envelope)
    {
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (string.IsNullOrWhiteSpace(envelope.IdempotencyKey)
            || envelope.IdempotencyKey.Length is < 8 or > 128
            || !IsSafeHeaderValue(envelope.IdempotencyKey)
            || string.IsNullOrWhiteSpace(envelope.Nonce)
            || envelope.Nonce.Length is < 16 or > 128
            || !IsSafeHeaderValue(envelope.Nonce))
        {
            return Fail<T>(
                EdgeFailure.Validation,
                "edge.envelope_invalid",
                "A valid idempotency key and nonce are required.");
        }

        if ((now - envelope.TimestampUtc).Duration()
            > TimeSpan.FromSeconds(options.Value.TimestampToleranceSeconds))
        {
            return Fail<T>(
                EdgeFailure.Forbidden,
                "edge.timestamp_outside_tolerance",
                "The device request timestamp is outside the accepted tolerance.");
        }

        return null;
    }

    private void AddEdgeAudit(
        string eventType,
        Guid deviceId,
        Guid? targetId,
        string? ipAddress,
        DateTimeOffset occurredAtUtc,
        string? correlationId)
    {
        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            TenantId = edgeRequestContext.TenantId ?? requestContext.TenantId,
            ActorUserId = requestContext.UserId,
            ActorDeviceId = deviceId,
            EventType = eventType,
            Outcome = "Succeeded",
            TargetType = "EdgeDevice",
            TargetId = targetId?.ToString() ?? deviceId.ToString(),
            IsSupportContext = requestContext.IsSupportContext,
            OccurredAtUtc = occurredAtUtc,
            IpAddress = ipAddress ?? string.Empty,
            CorrelationId = correlationId,
        });
    }

    private static bool Matches(
        EdgeCommand command,
        string commandType,
        string requestHash,
        EdgeRequestEnvelope envelope) =>
        command.CommandType == commandType
        && command.RequestHash == requestHash
        && command.IdempotencyKey == envelope.IdempotencyKey;

    private static bool IsSafeHeaderValue(string value) => value.All(character =>
        char.IsAsciiLetterOrDigit(character)
        || character is '.' or ':' or '_' or '-');

    private static string Hash(params object[] values) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(values))));

    private static string Version(byte[] value) => Convert.ToBase64String(value);

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "duplicate",
            StringComparison.OrdinalIgnoreCase) == true
        || exception.InnerException?.Message.Contains(
            "UNIQUE",
            StringComparison.OrdinalIgnoreCase) == true;

    private static EdgeResult<T> Validation<T>(DomainError error) =>
        Fail<T>(EdgeFailure.Validation, error.Code, error.Description);

    private static EdgeResult<T> NotFound<T>() =>
        Fail<T>(
            EdgeFailure.NotFound,
            "edge_devices.not_found",
            "The requested edge device was not found.");

    private static EdgeResult<T> Fail<T>(
        EdgeFailure failure,
        string code,
        string message) => EdgeResult<T>.Fail(failure, code, message);

    private sealed record EdgeContextLoad(
        EdgeDevice? Device,
        EdgeDeviceCredential? Credential,
        EdgeFailure Failure,
        string Code,
        string Message)
    {
        public static EdgeContextLoad Success(
            EdgeDevice device,
            EdgeDeviceCredential credential) =>
            new(device, credential, EdgeFailure.None, string.Empty, string.Empty);

        public static EdgeContextLoad Rejected(
            EdgeFailure failure,
            string code,
            string message) =>
            new(null, null, failure, code, message);
    }

    private sealed record CommandStart(
        EdgeCommand? Command,
        EdgeCommand? Existing,
        bool IsReplay,
        EdgeFailure Failure,
        string Code,
        string Message)
    {
        public static CommandStart Created(EdgeCommand command) =>
            new(command, null, false, EdgeFailure.None, string.Empty, string.Empty);

        public static CommandStart Replay(EdgeCommand command) =>
            new(null, command, true, EdgeFailure.None, string.Empty, string.Empty);

        public static CommandStart Rejected(
            EdgeFailure failure,
            string code,
            string message) =>
            new(null, null, false, failure, code, message);
    }

    private CommandStart AddAndReturnCommand(EdgeCommand command)
    {
        dbContext.EdgeCommands.Add(command);
        return CommandStart.Created(command);
    }
}
