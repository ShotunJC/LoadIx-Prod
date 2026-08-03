using LoadIx.Application.Documents;
using LoadIx.Infrastructure.Resilience;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace LoadIx.Infrastructure.Documents;

public sealed class MinioObjectStorage(
    IMinioClient client,
    IOptions<DocumentStorageOptions> options,
    TransientOperationExecutor operations) : IObjectStorage
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        await operations.ExecuteAsync(
            "object-storage",
            async token =>
            {
                var exists = await client.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(_bucket),
                    token);
                if (!exists)
                {
                    await client.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(_bucket),
                        token);
                }
            },
            cancellationToken);
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

        await operations.ExecuteAsync(
            "object-storage",
            async token =>
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
                    token);
            },
            cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        string key,
        CancellationToken cancellationToken)
    {
        return await operations.ExecuteAsync(
            "object-storage",
            async token =>
            {
                var memory = new MemoryStream();
                try
                {
                    await client.GetObjectAsync(
                        new GetObjectArgs()
                            .WithBucket(_bucket)
                            .WithObject(key)
                            .WithCallbackStream(async stream =>
                                await stream.CopyToAsync(memory, token)),
                        token);
                    memory.Position = 0;
                    return (Stream)memory;
                }
                catch
                {
                    await memory.DisposeAsync();
                    throw;
                }
            },
            cancellationToken);
    }

    public Task DeleteAsync(
        string key,
        CancellationToken cancellationToken) =>
        operations.ExecuteAsync(
            "object-storage",
            token => client.RemoveObjectAsync(
                new RemoveObjectArgs().WithBucket(_bucket).WithObject(key),
                token),
            cancellationToken);
}
