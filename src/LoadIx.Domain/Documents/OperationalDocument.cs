using LoadIx.Domain.Common;

namespace LoadIx.Domain.Documents;

public sealed class OperationalDocument :
    AggregateRoot<Guid>,
    ITenantOwned
{
    private OperationalDocument(
        Guid id,
        TenantId tenantId,
        DocumentOwnerType ownerType,
        Guid ownerId,
        DocumentType documentType,
        string displayFileName,
        string contentType,
        long sizeBytes,
        string checksum,
        string storageKey,
        string? previewStorageKey,
        Guid uploaderUserId,
        DateTimeOffset uploadedAtUtc,
        string? notes)
        : base(id)
    {
        TenantId = tenantId;
        OwnerType = ownerType;
        OwnerId = ownerId;
        DocumentType = documentType;
        DisplayFileName = displayFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256Checksum = checksum;
        StorageKey = storageKey;
        PreviewStorageKey = previewStorageKey;
        UploaderUserId = uploaderUserId;
        UploadedAtUtc = uploadedAtUtc.ToUniversalTime();
        Notes = notes?.Trim() ?? string.Empty;
        Status = DocumentStatus.Available;
    }

    private OperationalDocument()
    {
        DisplayFileName = string.Empty;
        ContentType = string.Empty;
        Sha256Checksum = string.Empty;
        StorageKey = string.Empty;
        Notes = string.Empty;
    }

    public TenantId TenantId { get; private set; }

    public DocumentOwnerType OwnerType { get; private set; }

    public Guid OwnerId { get; private set; }

    public DocumentType DocumentType { get; private set; }

    public string DisplayFileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public string Sha256Checksum { get; private set; }

    public string StorageKey { get; private set; }

    public string? PreviewStorageKey { get; private set; }

    public Guid UploaderUserId { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }

    public string Notes { get; private set; }

    public DocumentStatus Status { get; private set; }

    public Guid? DeletedByUserId { get; private set; }

    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static Result<OperationalDocument> Create(
        Guid id,
        TenantId tenantId,
        DocumentOwnerType ownerType,
        Guid ownerId,
        DocumentType documentType,
        string displayFileName,
        string contentType,
        long sizeBytes,
        string checksum,
        string storageKey,
        string? previewStorageKey,
        Guid uploaderUserId,
        DateTimeOffset uploadedAtUtc,
        string? notes)
    {
        if (id == Guid.Empty || ownerId == Guid.Empty || uploaderUserId == Guid.Empty)
        {
            return Invalid(
                "document.identity_required",
                "Document, owner, and uploader identifiers are required.");
        }

        if (tenantId == default)
        {
            return Invalid(
                "document.tenant_required",
                "A document tenant is required.");
        }

        if (string.IsNullOrWhiteSpace(displayFileName)
            || displayFileName.Length > 160)
        {
            return Invalid(
                "document.file_name_invalid",
                "The display file name is required and cannot exceed 160 characters.");
        }

        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 100)
        {
            return Invalid(
                "document.content_type_invalid",
                "A valid content type is required.");
        }

        if (sizeBytes <= 0)
        {
            return Invalid(
                "document.size_invalid",
                "Document size must be positive.");
        }

        if (checksum.Length != 64
            || checksum.Any(character => !Uri.IsHexDigit(character)))
        {
            return Invalid(
                "document.checksum_invalid",
                "A SHA-256 checksum is required.");
        }

        if (string.IsNullOrWhiteSpace(storageKey)
            || storageKey.Length > 500
            || storageKey.Contains("..", StringComparison.Ordinal))
        {
            return Invalid(
                "document.storage_key_invalid",
                "A server-generated storage key is required.");
        }

        if (notes?.Length > 1000)
        {
            return Invalid(
                "document.notes_too_long",
                "Document notes cannot exceed 1000 characters.");
        }

        return Result<OperationalDocument>.Success(new OperationalDocument(
            id,
            tenantId,
            ownerType,
            ownerId,
            documentType,
            displayFileName.Trim(),
            contentType.Trim().ToLowerInvariant(),
            sizeBytes,
            checksum.ToUpperInvariant(),
            storageKey,
            previewStorageKey,
            uploaderUserId,
            uploadedAtUtc,
            notes));
    }

    public Result Delete(
        Guid actorUserId,
        DateTimeOffset deletedAtUtc,
        bool ownerIsFinalised)
    {
        if (Status == DocumentStatus.Deleted)
        {
            return Result.Success();
        }

        if (ownerIsFinalised)
        {
            return Result.Failure(new DomainError(
                "document.owner_finalised",
                "Documents cannot be deleted after the owning trip is completed."));
        }

        if (actorUserId == Guid.Empty)
        {
            return Result.Failure(new DomainError(
                "document.actor_required",
                "A deletion actor is required."));
        }

        Status = DocumentStatus.Deleted;
        DeletedByUserId = actorUserId;
        DeletedAtUtc = deletedAtUtc.ToUniversalTime();
        return Result.Success();
    }

    private static Result<OperationalDocument> Invalid(
        string code,
        string message) =>
        Result<OperationalDocument>.Failure(new DomainError(code, message));
}
