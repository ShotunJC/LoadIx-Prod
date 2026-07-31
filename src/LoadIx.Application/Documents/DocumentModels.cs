namespace LoadIx.Application.Documents;

public sealed record DocumentUploadModel(
    string OwnerType,
    Guid OwnerId,
    string DocumentType,
    string? Notes,
    string FileName,
    string DeclaredContentType,
    long DeclaredSize);

public sealed record DocumentItem(
    Guid Id,
    string OwnerType,
    Guid OwnerId,
    string DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Checksum,
    Guid UploaderUserId,
    DateTimeOffset UploadedAtUtc,
    string Notes,
    bool HasPreview,
    bool CanDelete,
    string Version);

public sealed record DocumentContent(
    Stream Content,
    string ContentType,
    long SizeBytes,
    string FileName,
    string Sha256Checksum,
    bool IsPreview) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public enum DocumentFailure
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict,
    StorageUnavailable,
}

public sealed record DocumentResult<T>(
    T? Value,
    DocumentFailure Failure,
    string Code,
    string Message)
{
    public bool IsSuccess => Failure == DocumentFailure.None;
}

public static class DocumentResults
{
    public static DocumentResult<T> Success<T>(T value) =>
        new(value, DocumentFailure.None, string.Empty, string.Empty);

    public static DocumentResult<T> Fail<T>(
        DocumentFailure failure,
        string code,
        string message) =>
        new(default, failure, code, message);
}

public enum MalwareScanOutcome
{
    Clean,
    Infected,
    Unavailable,
}

public sealed record MalwareScanResult(
    MalwareScanOutcome Outcome,
    string? DetectionName = null);
