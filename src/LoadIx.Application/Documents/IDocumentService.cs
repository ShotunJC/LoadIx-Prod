namespace LoadIx.Application.Documents;

public interface IDocumentService
{
    Task<DocumentResult<IReadOnlyList<DocumentItem>>> ListAsync(
        string ownerType,
        Guid ownerId,
        CancellationToken cancellationToken);

    Task<DocumentResult<DocumentItem>> UploadAsync(
        DocumentUploadModel model,
        Stream content,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<DocumentResult<DocumentContent>> DownloadAsync(
        Guid documentId,
        bool preview,
        CancellationToken cancellationToken);

    Task<DocumentResult<bool>> DeleteAsync(
        Guid documentId,
        string version,
        CancellationToken cancellationToken);
}

public interface IObjectStorage
{
    Task EnsureReadyAsync(CancellationToken cancellationToken);

    Task PutAsync(
        string key,
        Stream content,
        long sizeBytes,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        string key,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string key,
        CancellationToken cancellationToken);
}

public interface IMalwareScanner
{
    Task<MalwareScanResult> ScanAsync(
        Stream content,
        CancellationToken cancellationToken);
}
