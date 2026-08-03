using System.Text.Json;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Governance;
using LoadIx.Application.Security;
using LoadIx.Domain.Common;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LoadIx.Infrastructure.Governance;

public sealed class DataGovernanceService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    SecurityAuditWriter auditWriter,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IDataGovernanceService
{
    private const int DefaultOperationalRetentionDays = 365;
    private const int DefaultAuditRetentionDays = 2_555;
    private const int DefaultDocumentMetadataRetentionDays = 365;
    private const int MaximumExportRowsPerCollection = 50_000;

    private static readonly JsonSerializerOptions ExportJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };

    public async Task<GovernanceResult<RetentionPolicyModel>> GetRetentionPolicyAsync(
        CancellationToken cancellationToken)
    {
        var denied = Authorize<RetentionPolicyModel>(LoadIxPermissions.TenantManage);
        if (denied is not null)
        {
            return denied;
        }

        var policy = await dbContext.TenantRetentionPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return GovernanceResult<RetentionPolicyModel>.Success(
            policy is null
                ? new RetentionPolicyModel(
                    null,
                    DefaultOperationalRetentionDays,
                    DefaultAuditRetentionDays,
                    DefaultDocumentMetadataRetentionDays,
                    false,
                    null,
                    string.Empty)
                : Map(policy));
    }

    public async Task<GovernanceResult<RetentionPolicyModel>> UpdateRetentionPolicyAsync(
        RetentionPolicyUpdateModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<RetentionPolicyModel>(LoadIxPermissions.TenantManage);
        if (denied is not null)
        {
            return denied;
        }

        var validation = ValidateRetention<RetentionPolicyModel>(model);
        if (validation is not null)
        {
            return validation;
        }

        var tenantId = RequiredTenant();
        var policy = await dbContext.TenantRetentionPolicies
            .SingleOrDefaultAsync(cancellationToken);
        if (policy is null)
        {
            policy = TenantRetentionPolicy.Create(
                Guid.NewGuid(),
                tenantId,
                model.OperationalDataRetentionDays,
                model.AuditRetentionDays,
                model.DocumentMetadataRetentionDays,
                model.LegalHold,
                model.LegalHoldReason);
            dbContext.TenantRetentionPolicies.Add(policy);
        }
        else
        {
            if (!ApplyVersion(policy, model.Version))
            {
                return GovernanceResult<RetentionPolicyModel>.Fail(
                    GovernanceFailure.Validation,
                    "concurrency.version_required",
                    "A valid retention policy version is required for an update.");
            }

            policy.Update(
                model.OperationalDataRetentionDays,
                model.AuditRetentionDays,
                model.DocumentMetadataRetentionDays,
                model.LegalHold,
                model.LegalHoldReason);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = Map(policy);
        await auditWriter.WriteAsync(
            "Governance.RetentionPolicyChanged",
            "Succeeded",
            "TenantRetentionPolicy",
            policy.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(response),
            reason: model.LegalHoldReason);
        return GovernanceResult<RetentionPolicyModel>.Success(response);
    }

    public IReadOnlyList<PersonalDataInventoryItem> GetPersonalDataInventory() =>
    [
        new(
            "Identity and contact",
            "User name, email, display name, phone number, invitations",
            "Authentication, account administration, notifications",
            "Identity.Users and Identity.UserInvitations",
            "Tenant retention policy; invitations expire after their configured validity",
            "Deactivate first, then replace direct identifiers with an anonymised marker"),
        new(
            "Operational records",
            "Customers, locations, contracts, load instructions, trips, assignments",
            "Transport execution, traceability, reconciliation and reporting",
            "Tenant-scoped operational tables",
            "Operational retention policy, subject to holds and financial dependencies",
            "Preserve operational history where needed; remove or anonymise direct personal references only"),
        new(
            "Financial records",
            "Costs, approvals, invoice batches and profitability",
            "Financial preparation, approvals and invoicing",
            "Tenant-scoped finance tables",
            "Controlled by retention policy and financial/legal holds",
            "Do not delete through the self-service workflow; retain approval history"),
        new(
            "Document metadata",
            "File name, type, checksum, size, owner and access events",
            "Proof, compliance evidence and controlled download",
            "Tenant-scoped document metadata and object storage",
            "Document metadata retention policy",
            "Anonymise personal labels where appropriate; object bodies require a separately approved process"),
        new(
            "Security and audit",
            "Audit actions, actor references, correlation and safe summaries",
            "Accountability, incident response and support traceability",
            "Audit.SecurityAuditEvents",
            "Audit retention policy; legal holds suspend automated disposal",
            "Audit records are immutable; this workflow does not delete or rewrite them"),
    ];

    public async Task<GovernanceResult<TenantExportResult>> ExportTenantDataAsync(
        CancellationToken cancellationToken)
    {
        var denied = Authorize<TenantExportResult>(LoadIxPermissions.TenantDataExport);
        if (denied is not null)
        {
            return denied;
        }

        var tenantId = RequiredTenant();
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return GovernanceResult<TenantExportResult>.Fail(
                GovernanceFailure.NotFound,
                "tenant.not_found",
                "The current tenant was not found.");
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId)
            .OrderBy(user => user.Id)
            .Take(MaximumExportRowsPerCollection)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.PhoneNumber,
                user.IsActive,
                user.EmailConfirmed,
                user.CreatedAtUtc,
                user.ModifiedAtUtc,
            })
            .ToListAsync(cancellationToken);
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.Id)
            .Take(MaximumExportRowsPerCollection)
            .Select(role => new
            {
                role.Id,
                role.DisplayName,
                role.Permissions,
            })
            .ToListAsync(cancellationToken);
        var customers = await dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Id)
            .Take(MaximumExportRowsPerCollection)
            .Select(customer => new
            {
                customer.Id,
                customer.LegalName,
                customer.TradingName,
                customer.RegistrationNumber,
                customer.TaxNumber,
                customer.PaymentTermsDays,
                customer.IsActive,
                customer.Notes,
            })
            .ToListAsync(cancellationToken);
        var trips = await dbContext.Trips
            .AsNoTracking()
            .OrderBy(trip => trip.Id)
            .Take(MaximumExportRowsPerCollection)
            .Select(trip => new
            {
                trip.Id,
                trip.TripNumber,
                trip.Status,
                trip.CustomerId,
                trip.DriverId,
                trip.VehicleId,
                trip.PlannedQuantity,
                trip.SourceGrossWeight,
                trip.SourceTareWeight,
                trip.SourceWeighbridgeReference,
                trip.DestinationGrossWeight,
                trip.DestinationTareWeight,
                trip.DestinationWeighbridgeReference,
                trip.IsReconciled,
                trip.HasRequiredDocuments,
            })
            .ToListAsync(cancellationToken);
        var documents = await dbContext.Documents
            .AsNoTracking()
            .OrderBy(document => document.Id)
            .Take(MaximumExportRowsPerCollection)
            .Select(document => new
            {
                document.Id,
                document.OwnerType,
                document.OwnerId,
                document.DocumentType,
                document.DisplayFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256Checksum,
                document.UploaderUserId,
                document.UploadedAtUtc,
                document.Notes,
                document.Status,
                document.DeletedByUserId,
                document.DeletedAtUtc,
            })
            .ToListAsync(cancellationToken);
        var financials = await dbContext.TripFinancials
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Take(MaximumExportRowsPerCollection)
            .Select(item => new
            {
                item.Id,
                item.TripId,
                item.TripNumber,
                item.CustomerId,
                item.CurrencyCode,
                item.BillableTonnes,
                item.TaxExclusiveAmount,
                item.TaxAmount,
                item.Total,
                item.ApprovalStatus,
                item.ApprovedByUserId,
                item.ApprovedAtUtc,
                item.ApprovalReason,
                item.InvoiceBatchId,
            })
            .ToListAsync(cancellationToken);
        var auditEvents = await dbContext.SecurityAuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.TenantId == tenantId)
            .OrderBy(auditEvent => auditEvent.OccurredAtUtc)
            .ThenBy(auditEvent => auditEvent.Id)
            .Take(MaximumExportRowsPerCollection)
            .Select(auditEvent => new
            {
                auditEvent.Id,
                auditEvent.ActorUserId,
                auditEvent.ActorDeviceId,
                auditEvent.EventType,
                auditEvent.Outcome,
                auditEvent.TargetType,
                auditEvent.TargetId,
                auditEvent.IsSupportContext,
                auditEvent.OccurredAtUtc,
                auditEvent.CorrelationId,
                auditEvent.RequestMetadataJson,
                auditEvent.BeforeSummaryJson,
                auditEvent.AfterSummaryJson,
                auditEvent.Reason,
            })
            .ToListAsync(cancellationToken);

        var export = new
        {
            schemaVersion = "1",
            exportedAtUtc = timeProvider.GetUtcNow(),
            tenant = new
            {
                tenant.Id,
                tenant.Name,
                tenant.LegalName,
                tenant.RegistrationNumber,
                tenant.TaxNumber,
                tenant.DefaultCurrency,
                tenant.TimeZone,
                tenant.IsActive,
            },
            users,
            roles,
            customers,
            trips,
            documents,
            financials,
            auditEvents,
            excluded = new
            {
                passwords = true,
                accessTokens = true,
                refreshTokens = true,
                deviceCredentials = true,
                documentBodies = true,
                objectStorageKeys = true,
            },
        };
        var content = JsonSerializer.SerializeToUtf8Bytes(
            export,
            ExportJsonOptions);
        var fileName = $"loadix-tenant-{tenantId.Value:N}-export-{timeProvider.GetUtcNow():yyyyMMddHHmmss}.json";
        await auditWriter.WriteAsync(
            "Export.TenantData",
            "Succeeded",
            "Tenant",
            tenantId.Value.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            afterSummaryJson: AuditSummary.Serialize(new
            {
                schemaVersion = "1",
                byteCount = content.Length,
                users = users.Count,
                trips = trips.Count,
                documents = documents.Count,
                financials = financials.Count,
                auditEvents = auditEvents.Count,
            }));
        return GovernanceResult<TenantExportResult>.Success(
            new TenantExportResult(content, fileName));
    }

    public async Task<GovernanceResult<AnonymisationRequestResponse>> RequestAnonymisationAsync(
        AnonymisationRequestModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<AnonymisationRequestResponse>(LoadIxPermissions.UserManage);
        if (denied is not null)
        {
            return denied;
        }

        if (model.UserId == Guid.Empty || string.IsNullOrWhiteSpace(model.Reason)
            || model.Reason.Trim().Length > 500)
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.Validation,
                "anonymisation.request_invalid",
                "A target user and a reason of no more than 500 characters are required.");
        }

        var tenantId = RequiredTenant();
        if (!await dbContext.Users.AnyAsync(
                user => user.Id == model.UserId && user.TenantId == tenantId,
                cancellationToken))
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.NotFound,
                "user.not_found",
                "The requested user was not found.");
        }

        if (await dbContext.AnonymisationRequests.AnyAsync(
                request => request.UserId == model.UserId
                    && request.Status == AnonymisationRequestStatus.Pending,
                cancellationToken))
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.Conflict,
                "anonymisation.request_exists",
                "A pending anonymisation request already exists for this user.");
        }

        var request = AnonymisationRequest.Create(
            Guid.NewGuid(),
            tenantId,
            model.UserId,
            model.Reason,
            requestContext.UserId!.Value,
            timeProvider.GetUtcNow());
        dbContext.AnonymisationRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "Governance.AnonymisationRequested",
            "Succeeded",
            "User",
            model.UserId.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            reason: model.Reason);
        return GovernanceResult<AnonymisationRequestResponse>.Success(Map(request));
    }

    public async Task<GovernanceResult<IReadOnlyList<AnonymisationRequestResponse>>> ListAnonymisationRequestsAsync(
        CancellationToken cancellationToken)
    {
        var denied = Authorize<IReadOnlyList<AnonymisationRequestResponse>>(
            LoadIxPermissions.UserManage);
        if (denied is not null)
        {
            return denied;
        }

        var requests = await dbContext.AnonymisationRequests
            .AsNoTracking()
            .OrderByDescending(request => request.RequestedAtUtc)
            .ThenBy(request => request.Id)
            .Take(200)
            .ToListAsync(cancellationToken);
        return GovernanceResult<IReadOnlyList<AnonymisationRequestResponse>>.Success(
            requests.Select(Map).ToArray());
    }

    public async Task<GovernanceResult<AnonymisationRequestResponse>> ApproveAnonymisationAsync(
        Guid requestId,
        AnonymisationApprovalModel model,
        CancellationToken cancellationToken)
    {
        var denied = Authorize<AnonymisationRequestResponse>(LoadIxPermissions.UserManage);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(model.ReviewReason)
            || model.ReviewReason.Trim().Length > 500)
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.Validation,
                "anonymisation.review_reason_required",
                "A review reason of no more than 500 characters is required.");
        }

        var tenantId = RequiredTenant();
        var request = await dbContext.AnonymisationRequests
            .SingleOrDefaultAsync(
                candidate => candidate.Id == requestId
                    && candidate.TenantId == tenantId,
                cancellationToken);
        if (request is null)
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.NotFound,
                "anonymisation.request_not_found",
                "The anonymisation request was not found.");
        }

        if (request.Status != AnonymisationRequestStatus.Pending)
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.Conflict,
                "anonymisation.request_already_completed",
                "The anonymisation request has already been completed.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UserId
                && candidate.TenantId == tenantId,
            cancellationToken);
        if (user is null)
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.NotFound,
                "user.not_found",
                "The user associated with the request was not found.");
        }

        if (user.IsActive)
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.Conflict,
                "anonymisation.deactivation_required",
                "Deactivate the user before approving anonymisation.");
        }

        var originalEmail = user.Email;
        var originalDisplayName = user.DisplayName;
        var anonymisedEmail = $"anonymized-{user.Id:N}@invalid.loadix";
        user.UserName = anonymisedEmail;
        user.NormalizedUserName = userManager.NormalizeName(anonymisedEmail);
        user.Email = anonymisedEmail;
        user.NormalizedEmail = userManager.NormalizeEmail(anonymisedEmail);
        user.DisplayName = "Anonymised user";
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.IsActive = false;
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return GovernanceResult<AnonymisationRequestResponse>.Fail(
                GovernanceFailure.Conflict,
                "anonymisation.update_failed",
                "The user could not be anonymised safely.");
        }

        var now = timeProvider.GetUtcNow();
        await dbContext.RefreshSessions
            .Where(session => session.UserId == user.Id && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.RevokedAtUtc, now),
                cancellationToken);
        request.Complete(
            requestContext.UserId!.Value,
            now,
            model.ReviewReason);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            "Governance.UserAnonymised",
            "Succeeded",
            "User",
            user.Id.ToString(),
            tenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken,
            beforeSummaryJson: AuditSummary.Serialize(new
            {
                email = originalEmail,
                displayName = originalDisplayName,
            }),
            afterSummaryJson: AuditSummary.Serialize(new
            {
                displayName = user.DisplayName,
                isActive = user.IsActive,
            }),
            reason: model.ReviewReason);
        return GovernanceResult<AnonymisationRequestResponse>.Success(Map(request));
    }

    private static GovernanceResult<T>? ValidateRetention<T>(
        RetentionPolicyUpdateModel model)
    {
        if (model.OperationalDataRetentionDays is < 1 or > 36_500
            || model.AuditRetentionDays is < 1 or > 36_500
            || model.DocumentMetadataRetentionDays is < 1 or > 36_500)
        {
            return GovernanceResult<T>.Fail(
                GovernanceFailure.Validation,
                "retention.values_invalid",
                "Retention values must be between 1 and 36,500 days.");
        }

        if (model.LegalHold
            && string.IsNullOrWhiteSpace(model.LegalHoldReason))
        {
            return GovernanceResult<T>.Fail(
                GovernanceFailure.Validation,
                "retention.legal_hold_reason_required",
                "A legal-hold reason of no more than 500 characters is required.");
        }

        if (model.LegalHoldReason?.Trim().Length > 500)
        {
            return GovernanceResult<T>.Fail(
                GovernanceFailure.Validation,
                "retention.legal_hold_reason_invalid",
                "A legal-hold reason must be no more than 500 characters.");
        }

        return null;
    }

    private GovernanceResult<T>? Authorize<T>(string permission) =>
        !requestContext.TenantId.HasValue
            || !requestContext.UserId.HasValue
            || !requestContext.HasPermission(permission)
        ? GovernanceResult<T>.Fail(
            GovernanceFailure.Forbidden,
            "authorization.denied",
            "You do not have permission to perform this operation.")
        : null;

    private TenantId RequiredTenant() =>
        requestContext.TenantId
        ?? throw new InvalidOperationException(
            "An authenticated tenant context is required.");

    private bool ApplyVersion(TenantRetentionPolicy policy, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        try
        {
            dbContext.Entry(policy).Property(nameof(policy.RowVersion)).OriginalValue =
                Convert.FromBase64String(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static RetentionPolicyModel Map(TenantRetentionPolicy policy) =>
        new(
            policy.Id,
            policy.OperationalDataRetentionDays,
            policy.AuditRetentionDays,
            policy.DocumentMetadataRetentionDays,
            policy.LegalHold,
            policy.LegalHoldReason,
            Convert.ToBase64String(policy.RowVersion));

    private static AnonymisationRequestResponse Map(AnonymisationRequest request) =>
        new(
            request.Id,
            request.UserId,
            request.Status.ToString(),
            request.Reason,
            request.RequestedByUserId,
            request.RequestedAtUtc,
            request.ReviewedByUserId,
            request.ReviewedAtUtc,
            request.ReviewReason,
            request.CompletedAtUtc,
            Convert.ToBase64String(request.RowVersion));
}
