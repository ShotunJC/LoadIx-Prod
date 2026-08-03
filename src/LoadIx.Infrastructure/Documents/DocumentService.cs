using System.Security.Cryptography;
using System.Text;
using ImageMagick;
using LoadIx.Application.Abstractions;
using LoadIx.Application.Documents;
using LoadIx.Application.Security;
using LoadIx.Domain.Documents;
using LoadIx.Domain.Trips;
using LoadIx.Infrastructure.Identity;
using LoadIx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoadIx.Infrastructure.Documents;

public sealed partial class DocumentService(
    LoadIxDbContext dbContext,
    ICurrentRequestContext requestContext,
    IObjectStorage objectStorage,
    IMalwareScanner malwareScanner,
    SecurityAuditWriter auditWriter,
    TimeProvider timeProvider,
    IOptions<DocumentStorageOptions> options,
    ILogger<DocumentService> logger) : IDocumentService
{
    private static readonly Dictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        };

    public async Task<DocumentResult<IReadOnlyList<DocumentItem>>> ListAsync(
        string ownerType,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        if (!TryOwner(ownerType, ownerId, out var parsedOwner, out var error))
        {
            return Fail<IReadOnlyList<DocumentItem>>(
                DocumentFailure.Validation,
                "document.owner_invalid",
                error ?? "A valid document owner is required.");
        }

        var access = await OwnerAccessAsync(
            parsedOwner,
            ownerId,
            false,
            null,
            cancellationToken);
        if (!access.Allowed)
        {
            return Fail<IReadOnlyList<DocumentItem>>(
                access.Exists ? DocumentFailure.Forbidden : DocumentFailure.NotFound,
                access.Exists ? "document.forbidden" : "document.owner_not_found",
                access.Exists
                    ? "You are not permitted to view documents for this record."
                    : "The document owner was not found.");
        }

        var documents = await dbContext.Documents
            .AsNoTracking()
            .Where(document =>
                document.OwnerType == parsedOwner
                && document.OwnerId == ownerId
                && document.Status == DocumentStatus.Available)
            .OrderByDescending(document => document.UploadedAtUtc)
            .ThenByDescending(document => document.Id)
            .ToListAsync(cancellationToken);
        var items = documents
            .Where(document => CanViewType(
                document.DocumentType,
                access.IsDriver,
                requestContext.HasPermission(LoadIxPermissions.FinanceView)))
            .Select(document => ToItem(
                document,
                access.CanManage
                && CanManageType(
                    document.DocumentType,
                    requestContext.HasPermission(LoadIxPermissions.FinanceManage))
                && !access.IsFinalised))
            .ToArray();
        return DocumentResults.Success<IReadOnlyList<DocumentItem>>(items);
    }

    public async Task<DocumentResult<DocumentItem>> UploadAsync(
        DocumentUploadModel model,
        Stream content,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryOwner(model.OwnerType, model.OwnerId, out var ownerType, out var error)
            || !Enum.TryParse<DocumentType>(
                model.DocumentType,
                true,
                out var documentType)
            || !Enum.IsDefined(documentType))
        {
            return Fail<DocumentItem>(
                DocumentFailure.Validation,
                "document.request_invalid",
                error ?? "The document type is invalid.");
        }

        if (!ValidIdempotencyKey(idempotencyKey))
        {
            return Fail<DocumentItem>(
                DocumentFailure.Validation,
                "document.idempotency_key_invalid",
                "Idempotency-Key is required and cannot exceed 128 characters.");
        }

        if (!TypeMatchesOwner(ownerType, documentType))
        {
            return Fail<DocumentItem>(
                DocumentFailure.Validation,
                "document.owner_type_mismatch",
                "The selected document type cannot be attached to this record.");
        }

        var access = await OwnerAccessAsync(
            ownerType,
            model.OwnerId,
            true,
            documentType,
            cancellationToken);
        if (!access.Allowed)
        {
            return Fail<DocumentItem>(
                access.Exists ? DocumentFailure.Forbidden : DocumentFailure.NotFound,
                access.Exists ? "document.forbidden" : "document.owner_not_found",
                access.Exists
                    ? "You are not permitted to upload this document."
                    : "The document owner was not found.");
        }

        if (access.IsFinalised)
        {
            return Fail<DocumentItem>(
                DocumentFailure.Conflict,
                "document.owner_finalised",
                "Documents are immutable after trip completion.");
        }

        var inspected = await InspectAsync(model, content, cancellationToken);
        if (!inspected.IsSuccess)
        {
            return Fail<DocumentItem>(
                DocumentFailure.Validation,
                inspected.Code,
                inspected.Message);
        }

        var file = inspected.Value!;
        var requestHash = Hash(string.Join(
            '|',
            ownerType,
            model.OwnerId,
            documentType,
            model.Notes?.Trim(),
            file.DisplayFileName,
            file.ContentType,
            file.Bytes.Length,
            file.Checksum));
        var actorId = requestContext.UserId!.Value;
        var existingOperation = await dbContext.DocumentUploadOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(operation =>
                    operation.ActorUserId == actorId
                    && operation.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existingOperation is not null)
        {
            if (!string.Equals(
                    existingOperation.RequestHash,
                    requestHash,
                    StringComparison.Ordinal))
            {
                return Fail<DocumentItem>(
                    DocumentFailure.Conflict,
                    "document.idempotency_key_reused",
                    "The idempotency key was already used for a different upload.");
            }

            var replay = await dbContext.Documents
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    document => document.Id == existingOperation.DocumentId,
                    cancellationToken);
            return replay is null
                ? Fail<DocumentItem>(
                    DocumentFailure.Conflict,
                    "document.replay_unavailable",
                    "The original upload result is no longer available.")
                : DocumentResults.Success(ToItem(
                    replay,
                    access.CanManage
                    && !access.IsFinalised
                    && replay.Status == DocumentStatus.Available));
        }

        var duplicate = await dbContext.Documents
            .SingleOrDefaultAsync(document =>
                    document.OwnerType == ownerType
                    && document.OwnerId == model.OwnerId
                    && document.DocumentType == documentType
                    && document.Sha256Checksum == file.Checksum
                    && document.Status == DocumentStatus.Available,
                cancellationToken);
        if (duplicate is not null)
        {
            await RecordOperationAsync(
                duplicate.Id,
                actorId,
                idempotencyKey,
                requestHash,
                cancellationToken);
            return DocumentResults.Success(ToItem(
                duplicate,
                access.CanManage && !access.IsFinalised));
        }

        var documentId = Guid.NewGuid();
        var tenantId = requestContext.TenantId!.Value;
        var extension = Path.GetExtension(file.DisplayFileName).ToLowerInvariant();
        var keyRoot =
            $"{tenantId.Value:N}/documents/{documentId:N}";
        var quarantineKey =
            $"{tenantId.Value:N}/quarantine/{Guid.NewGuid():N}{extension}";
        var storageKey = $"{keyRoot}/original{extension}";
        var previewKey = file.Preview is null ? null : $"{keyRoot}/preview.jpg";
        var storedKeys = new List<string>();

        try
        {
            await objectStorage.EnsureReadyAsync(cancellationToken);
            await PutAsync(
                quarantineKey,
                file.Bytes,
                file.ContentType,
                cancellationToken);
            storedKeys.Add(quarantineKey);
            await using var scanStream =
                new MemoryStream(file.Bytes, writable: false);
            var scan = await malwareScanner.ScanAsync(
                scanStream,
                cancellationToken);
            if (scan.Outcome == MalwareScanOutcome.Infected)
            {
                await AuditAsync(
                    "Document.UploadRejected",
                    "Rejected",
                    model.OwnerId,
                    cancellationToken);
                return Fail<DocumentItem>(
                    DocumentFailure.Validation,
                    "document.malware_detected",
                    "The file failed the malware safety check.");
            }

            if (scan.Outcome == MalwareScanOutcome.Unavailable)
            {
                return Fail<DocumentItem>(
                    DocumentFailure.StorageUnavailable,
                    "document.scanner_unavailable",
                    "Document scanning is temporarily unavailable.");
            }

            await PutAsync(
                storageKey,
                file.Bytes,
                file.ContentType,
                cancellationToken);
            storedKeys.Add(storageKey);
            if (file.Preview is not null && previewKey is not null)
            {
                await PutAsync(
                    previewKey,
                    file.Preview,
                    "image/jpeg",
                    cancellationToken);
                storedKeys.Add(previewKey);
            }

            await objectStorage.DeleteAsync(quarantineKey, cancellationToken);
            storedKeys.Remove(quarantineKey);

            var created = OperationalDocument.Create(
                documentId,
                tenantId,
                ownerType,
                model.OwnerId,
                documentType,
                file.DisplayFileName,
                file.ContentType,
                file.Bytes.Length,
                file.Checksum,
                storageKey,
                previewKey,
                actorId,
                timeProvider.GetUtcNow(),
                model.Notes);
            if (created.IsFailure)
            {
                return Fail<DocumentItem>(
                    DocumentFailure.Validation,
                    created.Error!.Code,
                    created.Error.Description);
            }

            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.Documents.Add(created.Value);
            dbContext.DocumentUploadOperations.Add(NewOperation(
                documentId,
                actorId,
                idempotencyKey,
                requestHash));
            await dbContext.SaveChangesAsync(cancellationToken);
            await AuditAsync(
                "Document.Uploaded",
                "Succeeded",
                documentId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            storedKeys.Clear();
            return DocumentResults.Success(ToItem(
                created.Value,
                access.CanManage));
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentOperation = await dbContext.DocumentUploadOperations
                .AsNoTracking()
                .SingleOrDefaultAsync(operation =>
                        operation.ActorUserId == actorId
                        && operation.IdempotencyKey == idempotencyKey,
                    CancellationToken.None);
            if (concurrentOperation is null)
            {
                return Fail<DocumentItem>(
                    DocumentFailure.StorageUnavailable,
                    "document.upload_failed",
                    "The document could not be stored safely.");
            }

            if (!string.Equals(
                    concurrentOperation.RequestHash,
                    requestHash,
                    StringComparison.Ordinal))
            {
                return Fail<DocumentItem>(
                    DocumentFailure.Conflict,
                    "document.idempotency_key_reused",
                    "The idempotency key was already used for a different upload.");
            }

            var concurrentDocument = await dbContext.Documents
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    document => document.Id == concurrentOperation.DocumentId,
                    CancellationToken.None);
            return concurrentDocument is null
                ? Fail<DocumentItem>(
                    DocumentFailure.Conflict,
                    "document.replay_unavailable",
                    "The original upload result is no longer available.")
                : DocumentResults.Success(ToItem(
                    concurrentDocument,
                    access.CanManage && !access.IsFinalised));
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            LogUploadFailed(logger, exception.GetType().Name, ownerType, model.OwnerId);
            return Fail<DocumentItem>(
                DocumentFailure.StorageUnavailable,
                "document.upload_failed",
                "The document could not be stored safely.");
        }
        finally
        {
            foreach (var key in storedKeys)
            {
                try
                {
                    await objectStorage.DeleteAsync(key, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    LogCleanupPending(logger, cleanupException.GetType().Name, Hash(key));
                }
            }
        }
    }

    public async Task<DocumentResult<DocumentContent>> DownloadAsync(
        Guid documentId,
        bool preview,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                    item.Id == documentId
                    && item.Status == DocumentStatus.Available,
                cancellationToken);
        if (document is null)
        {
            return Fail<DocumentContent>(
                DocumentFailure.NotFound,
                "document.not_found",
                "The document was not found.");
        }

        var access = await OwnerAccessAsync(
            document.OwnerType,
            document.OwnerId,
            false,
            document.DocumentType,
            cancellationToken);
        if (!access.Allowed
            || !CanViewType(
                document.DocumentType,
                access.IsDriver,
                requestContext.HasPermission(LoadIxPermissions.FinanceView)))
        {
            return Fail<DocumentContent>(
                DocumentFailure.Forbidden,
                "document.forbidden",
                "You are not permitted to download this document.");
        }

        var key = preview ? document.PreviewStorageKey : document.StorageKey;
        if (key is null)
        {
            return Fail<DocumentContent>(
                DocumentFailure.NotFound,
                "document.preview_unavailable",
                "A safe preview is not available for this document.");
        }

        try
        {
            var stream = await objectStorage.OpenReadAsync(key, cancellationToken);
            await AuditAsync(
                preview ? "Document.Previewed" : "Document.Downloaded",
                "Succeeded",
                document.Id,
                cancellationToken);
            return DocumentResults.Success(new DocumentContent(
                stream,
                preview ? "image/jpeg" : document.ContentType,
                preview ? stream.Length : document.SizeBytes,
                preview
                    ? $"{Path.GetFileNameWithoutExtension(document.DisplayFileName)}-preview.jpg"
                    : document.DisplayFileName,
                document.Sha256Checksum,
                preview));
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            LogOpenFailed(logger, exception.GetType().Name, documentId);
            return Fail<DocumentContent>(
                DocumentFailure.StorageUnavailable,
                "document.storage_unavailable",
                "The document is temporarily unavailable.");
        }
    }

    public async Task<DocumentResult<bool>> DeleteAsync(
        Guid documentId,
        string version,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents
            .SingleOrDefaultAsync(item =>
                    item.Id == documentId
                    && item.Status == DocumentStatus.Available,
                cancellationToken);
        if (document is null)
        {
            return Fail<bool>(
                DocumentFailure.NotFound,
                "document.not_found",
                "The document was not found.");
        }

        var access = await OwnerAccessAsync(
            document.OwnerType,
            document.OwnerId,
            true,
            document.DocumentType,
            cancellationToken);
        if (!access.Allowed)
        {
            return Fail<bool>(
                DocumentFailure.Forbidden,
                "document.forbidden",
                "You are not permitted to delete this document.");
        }

        if (!TryVersion(version, out var rowVersion)
            || !document.RowVersion.SequenceEqual(rowVersion))
        {
            return Fail<bool>(
                DocumentFailure.Conflict,
                "document.concurrency_conflict",
                "The document changed after it was loaded.");
        }

        var deleted = document.Delete(
            requestContext.UserId!.Value,
            timeProvider.GetUtcNow(),
            access.IsFinalised);
        if (deleted.IsFailure)
        {
            return Fail<bool>(
                DocumentFailure.Conflict,
                deleted.Error!.Code,
                deleted.Error.Description);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail<bool>(
                DocumentFailure.Conflict,
                "document.concurrency_conflict",
                "The document changed after it was loaded.");
        }

        await AuditAsync(
            "Document.Deleted",
            "Succeeded",
            document.Id,
            cancellationToken);
        await DeleteObjectBestEffortAsync(document.StorageKey);
        if (document.PreviewStorageKey is not null)
        {
            await DeleteObjectBestEffortAsync(document.PreviewStorageKey);
        }

        return DocumentResults.Success(true);
    }

    private async Task<InspectionResult> InspectAsync(
        DocumentUploadModel model,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (model.DeclaredSize <= 0
            || model.DeclaredSize > options.Value.MaximumSizeBytes)
        {
            return InspectionResult.Fail(
                "document.size_invalid",
                $"Files must be between 1 byte and {options.Value.MaximumSizeMegabytes} MB.");
        }

        if (model.Notes?.Length > 1000)
        {
            return InspectionResult.Fail(
                "document.notes_too_long",
                "Document notes cannot exceed 1000 characters.");
        }

        var displayName = SafeDisplayName(model.FileName);
        var extension = Path.GetExtension(displayName).ToLowerInvariant();
        if (!options.Value.AllowedExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase)
            || !ContentTypes.TryGetValue(extension, out var expectedContentType))
        {
            return InspectionResult.Fail(
                "document.extension_not_allowed",
                "Only PDF, JPEG, PNG, and WebP files are allowed.");
        }

        var declaredType = model.DeclaredContentType
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();
        if (!string.Equals(
                declaredType,
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return InspectionResult.Fail(
                "document.mime_type_mismatch",
                "The declared file type does not match its extension.");
        }

        using var memory = new MemoryStream();
        try
        {
            await CopyWithLimitAsync(
                content,
                memory,
                options.Value.MaximumSizeBytes,
                cancellationToken);
        }
        catch (InvalidDataException)
        {
            return InspectionResult.Fail(
                "document.size_invalid",
                $"Files cannot exceed {options.Value.MaximumSizeMegabytes} MB.");
        }
        var bytes = memory.ToArray();
        if (bytes.LongLength != model.DeclaredSize)
        {
            return InspectionResult.Fail(
                "document.size_mismatch",
                "The uploaded content length does not match the declared size.");
        }

        if (!SignatureMatches(extension, bytes) || LooksDangerous(bytes))
        {
            return InspectionResult.Fail(
                "document.signature_invalid",
                "The file signature does not match a permitted safe format.");
        }

        byte[]? preview = null;
        if (expectedContentType.StartsWith("image/", StringComparison.Ordinal))
        {
            try
            {
                var imageInfo = new MagickImageInfo(bytes);
                var sourcePixels = (long)imageInfo.Width * imageInfo.Height;
                if (sourcePixels > options.Value.MaximumImagePixels)
                {
                    return InspectionResult.Fail(
                        "document.image_dimensions_invalid",
                        "The image dimensions or frame count exceed the safety limit.");
                }

                using var images = new MagickImageCollection(bytes);
                if (images.Count != 1)
                {
                    return InspectionResult.Fail(
                        "document.image_dimensions_invalid",
                        "The image dimensions or frame count exceed the safety limit.");
                }

                var image = images[0];
                var pixels = (long)image.Width * image.Height;
                if (pixels > options.Value.MaximumImagePixels)
                {
                    return InspectionResult.Fail(
                        "document.image_dimensions_invalid",
                        "The image dimensions or frame count exceed the safety limit.");
                }

                image.AutoOrient();
                image.Resize(new MagickGeometry(
                    (uint)options.Value.PreviewMaximumDimension,
                    (uint)options.Value.PreviewMaximumDimension)
                {
                    Greater = true,
                });
                image.Strip();
                image.Format = MagickFormat.Jpeg;
                image.Quality = 82;
                preview = image.ToByteArray();
            }
            catch (MagickException)
            {
                return InspectionResult.Fail(
                    "document.image_invalid",
                    "The image could not be decoded safely.");
            }
        }

        return InspectionResult.Success(new InspectedFile(
            displayName,
            expectedContentType,
            bytes,
            Convert.ToHexString(SHA256.HashData(bytes)),
            preview));
    }

    private async Task<OwnerAccess> OwnerAccessAsync(
        DocumentOwnerType ownerType,
        Guid ownerId,
        bool mutate,
        DocumentType? documentType,
        CancellationToken cancellationToken)
    {
        var staffAllowed = requestContext.HasPermission(
            mutate
                ? LoadIxPermissions.DocumentManage
                : LoadIxPermissions.DocumentView);
        if (documentType == DocumentType.InvoiceSupportingDocument)
        {
            staffAllowed = staffAllowed
                && requestContext.HasPermission(
                    mutate
                        ? LoadIxPermissions.FinanceManage
                        : LoadIxPermissions.FinanceView);
        }

        var isDriver = requestContext.HasPermission(LoadIxPermissions.DocumentDriver)
            && requestContext.UserId.HasValue;
        var exists = false;
        var finalised = false;
        var belongsToDriver = false;

        switch (ownerType)
        {
            case DocumentOwnerType.Trip:
                var trip = await dbContext.Trips.AsNoTracking()
                    .Where(item => item.Id == ownerId)
                    .Select(item => new
                    {
                        item.Status,
                        item.DriverId,
                    })
                    .SingleOrDefaultAsync(cancellationToken);
                exists = trip is not null;
                finalised = trip?.Status == TripStatus.Completed;
                if (trip?.DriverId is not null && isDriver)
                {
                    belongsToDriver = await dbContext.Drivers.AsNoTracking()
                        .AnyAsync(driver =>
                                driver.Id == trip.DriverId
                                && driver.UserId == requestContext.UserId,
                            cancellationToken);
                }

                break;
            case DocumentOwnerType.LoadInstruction:
                exists = await dbContext.LoadInstructions.AsNoTracking()
                    .AnyAsync(item => item.Id == ownerId, cancellationToken);
                break;
            case DocumentOwnerType.Driver:
                var driverOwner = await dbContext.Drivers.AsNoTracking()
                    .Where(item => item.Id == ownerId)
                    .Select(item => new { item.UserId })
                    .SingleOrDefaultAsync(cancellationToken);
                exists = driverOwner is not null;
                belongsToDriver = isDriver
                    && driverOwner?.UserId == requestContext.UserId;
                break;
            case DocumentOwnerType.Vehicle:
                exists = await dbContext.Vehicles.AsNoTracking()
                    .AnyAsync(item => item.Id == ownerId, cancellationToken);
                break;
            case DocumentOwnerType.Trailer:
                exists = await dbContext.Trailers.AsNoTracking()
                    .AnyAsync(item => item.Id == ownerId, cancellationToken);
                break;
        }

        var driverTypeAllowed = documentType is null
            || documentType is DocumentType.CollectionWeighbridgeTicket
                or DocumentType.DeliveryWeighbridgeTicket
                or DocumentType.ProofOfDelivery
                or DocumentType.DriverDocument
                or DocumentType.Other;
        return new OwnerAccess(
            exists,
            exists && (staffAllowed || (belongsToDriver && driverTypeAllowed)),
            staffAllowed,
            belongsToDriver,
            finalised);
    }

    private static bool CanViewType(
        DocumentType type,
        bool isDriver,
        bool canViewFinance) =>
        type is not DocumentType.InvoiceSupportingDocument
        || (!isDriver && canViewFinance);

    private static bool CanManageType(
        DocumentType type,
        bool canManageFinance) =>
        type is not DocumentType.InvoiceSupportingDocument
        || canManageFinance;

    private static bool TypeMatchesOwner(
        DocumentOwnerType ownerType,
        DocumentType documentType) =>
        documentType switch
        {
            DocumentType.CollectionWeighbridgeTicket
                or DocumentType.DeliveryWeighbridgeTicket
                or DocumentType.ProofOfDelivery
                or DocumentType.InvoiceSupportingDocument =>
                ownerType == DocumentOwnerType.Trip,
            DocumentType.CustomerInstruction =>
                ownerType == DocumentOwnerType.LoadInstruction,
            DocumentType.DriverDocument =>
                ownerType == DocumentOwnerType.Driver,
            DocumentType.VehicleDocument =>
                ownerType == DocumentOwnerType.Vehicle,
            DocumentType.TrailerDocument =>
                ownerType == DocumentOwnerType.Trailer,
            DocumentType.Other => true,
            _ => false,
        };

    private async Task RecordOperationAsync(
        Guid documentId,
        Guid actorId,
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        dbContext.DocumentUploadOperations.Add(NewOperation(
            documentId,
            actorId,
            key,
            requestHash));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private DocumentUploadOperation NewOperation(
        Guid documentId,
        Guid actorId,
        string key,
        string requestHash) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = requestContext.TenantId!.Value,
            ActorUserId = actorId,
            IdempotencyKey = key,
            RequestHash = requestHash,
            DocumentId = documentId,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

    private async Task PutAsync(
        string key,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(bytes, writable: false);
        await objectStorage.PutAsync(
            key,
            stream,
            bytes.LongLength,
            contentType,
            cancellationToken);
    }

    private async Task DeleteObjectBestEffortAsync(string key)
    {
        try
        {
            await objectStorage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            LogCleanupPending(logger, exception.GetType().Name, Hash(key));
        }
    }

    private Task AuditAsync(
        string eventType,
        string outcome,
        Guid targetId,
        CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(
            eventType,
            outcome,
            "Document",
            targetId.ToString(),
            requestContext.TenantId,
            requestContext.UserId,
            requestContext.IsSupportContext,
            null,
            cancellationToken);

    private static DocumentItem ToItem(
        OperationalDocument document,
        bool canDelete) =>
        new(
            document.Id,
            document.OwnerType.ToString(),
            document.OwnerId,
            document.DocumentType.ToString(),
            document.DisplayFileName,
            document.ContentType,
            document.SizeBytes,
            document.Sha256Checksum,
            document.UploaderUserId,
            document.UploadedAtUtc,
            document.Notes,
            document.PreviewStorageKey is not null,
            canDelete,
            Convert.ToBase64String(document.RowVersion));

    private static bool TryOwner(
        string value,
        Guid ownerId,
        out DocumentOwnerType ownerType,
        out string? error)
    {
        ownerType = default;
        error = null;
        if (ownerId == Guid.Empty
            || !Enum.TryParse(value, true, out ownerType)
            || !Enum.IsDefined(ownerType))
        {
            error = "A valid document owner type and identifier are required.";
            return false;
        }

        return true;
    }

    private static string SafeDisplayName(string fileName)
    {
        var leaf = Path.GetFileName(
            (fileName ?? string.Empty).Replace('\\', '/'));
        var safe = new string(leaf
            .Where(character =>
                !char.IsControl(character)
                && (char.IsLetterOrDigit(character)
                    || character is ' ' or '.' or '-' or '_' or '(' or ')'))
            .ToArray())
            .Trim();
        if (safe.Length > 160)
        {
            var extension = Path.GetExtension(safe);
            safe = string.Concat(
                safe.AsSpan(0, Math.Min(140, safe.Length - extension.Length)),
                extension);
        }

        return string.IsNullOrWhiteSpace(safe) ? "document" : safe;
    }

    private static bool SignatureMatches(string extension, byte[] bytes) =>
        extension switch
        {
            ".pdf" => StartsWith(bytes, "%PDF-"u8),
            ".jpg" or ".jpeg" =>
                bytes.Length >= 3
                && bytes[0] == 0xFF
                && bytes[1] == 0xD8
                && bytes[2] == 0xFF,
            ".png" => StartsWith(
                bytes,
                [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
            ".webp" => bytes.Length >= 12
                && StartsWith(bytes, "RIFF"u8)
                && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false,
        };

    private static bool LooksDangerous(byte[] bytes)
    {
        if (StartsWith(bytes, "MZ"u8)
            || StartsWith(bytes, [0x7F, 0x45, 0x4C, 0x46])
            || StartsWith(bytes, "#!"u8)
            || StartsWith(bytes, "PK\x03\x04"u8))
        {
            return true;
        }

        var sample = Encoding.ASCII.GetString(
            bytes,
            0,
            Math.Min(bytes.Length, 1024));
        return sample.Contains("<script", StringComparison.OrdinalIgnoreCase)
            || sample.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || sample.Contains("<?php", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(byte[] bytes, ReadOnlySpan<byte> signature) =>
        bytes.AsSpan().StartsWith(signature);

    private static async Task CopyWithLimitAsync(
        Stream input,
        Stream output,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException(
                    "The uploaded document exceeds the configured size limit.");
            }

            await output.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);
        }
    }

    private static bool ValidIdempotencyKey(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => !char.IsControl(character));

    private static bool TryVersion(string version, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(version);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static DocumentResult<T> Fail<T>(
        DocumentFailure failure,
        string code,
        string message) =>
        DocumentResults.Fail<T>(failure, code, message);

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Error,
        Message = "Document upload failed for owner {OwnerType}/{OwnerId}; provider error {ExceptionType}.")]
    private static partial void LogUploadFailed(
        ILogger logger,
        string exceptionType,
        DocumentOwnerType ownerType,
        Guid ownerId);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Error,
        Message = "Document object {DocumentId} could not be opened; provider error {ExceptionType}.")]
    private static partial void LogOpenFailed(
        ILogger logger,
        string exceptionType,
        Guid documentId);

    [LoggerMessage(
        EventId = 5103,
        Level = LogLevel.Warning,
        Message = "Document object cleanup is pending with {ExceptionType}; storage key hash {StorageKeyHash}.")]
    private static partial void LogCleanupPending(
        ILogger logger,
        string exceptionType,
        string storageKeyHash);

    private sealed record OwnerAccess(
        bool Exists,
        bool Allowed,
        bool CanManage,
        bool IsDriver,
        bool IsFinalised);

    private sealed record InspectedFile(
        string DisplayFileName,
        string ContentType,
        byte[] Bytes,
        string Checksum,
        byte[]? Preview);

    private sealed record InspectionResult(
        InspectedFile? Value,
        string Code,
        string Message)
    {
        public bool IsSuccess => Value is not null;

        public static InspectionResult Success(InspectedFile value) =>
            new(value, string.Empty, string.Empty);

        public static InspectionResult Fail(string code, string message) =>
            new(null, code, message);
    }
}
