using System.Collections.Concurrent;

namespace LoadIx.Infrastructure.Documents;

public sealed class DocumentUploadCoordinator
{
    private readonly ConcurrentDictionary<string, Entry> entries = new(
        StringComparer.Ordinal);

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        string key,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = entries.GetOrAdd(key, static _ => new Entry());
            Interlocked.Increment(ref entry.References);
            if (entries.TryGetValue(key, out var current)
                && ReferenceEquals(entry, current))
            {
                try
                {
                    await entry.Semaphore.WaitAsync(cancellationToken);
                    return new Lease(this, key, entry);
                }
                catch
                {
                    ReleaseReference(key, entry);
                    throw;
                }
            }

            ReleaseReference(key, entry);
        }
    }

    private void Release(string key, Entry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(string key, Entry entry)
    {
        if (Interlocked.Decrement(ref entry.References) == 0)
        {
            entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int References;
    }

    private sealed class Lease(
        DocumentUploadCoordinator owner,
        string key,
        Entry entry) : IAsyncDisposable
    {
        private int released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                owner.Release(key, entry);
            }

            return ValueTask.CompletedTask;
        }
    }
}
