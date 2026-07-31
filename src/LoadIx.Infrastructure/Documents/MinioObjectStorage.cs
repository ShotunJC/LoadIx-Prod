using LoadIx.Application.Documents;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace LoadIx.Infrastructure.Documents;

public sealed class MinioObjectStorage(
    IMinioClient client,
    IOptions<DocumentStorageOptions> options) : IObjectStorage
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucket),
            cancellationToken);
        if (!exists)
        {
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucket),
                cancellationToken);
        }
    }

    public async Task PutAsync(
        string key,
        Stream content,
        long sizeBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithStreamData(content)
                .WithObjectSize(sizeBytes)
                .WithContentType(contentType),
            cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var memory = new MemoryStream();
        await client.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithCallbackStream(async stream =>
                    await stream.CopyToAsync(memory, cancellationToken)),
            cancellationToken);
        memory.Position = 0;
        return memory;
    }

    public Task DeleteAsync(
        string key,
        CancellationToken cancellationToken) =>
        client.RemoveObjectAsync(
            new RemoveObjectArgs().WithBucket(_bucket).WithObject(key),
            cancellationToken);
}
