using System.Text.Json;
using LoadIx.Application.Edge;

namespace LoadIx.EdgeSimulator;

public sealed record PendingReading(
    string IdempotencyKey,
    EdgeReadingModel Reading);

public sealed class PendingSubmissionStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };

    public async Task<IReadOnlyList<PendingReading>> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<PendingReading>>(
                   stream,
                   JsonOptions,
                   cancellationToken)
               ?? [];
    }

    public async Task AddAsync(
        PendingReading pending,
        CancellationToken cancellationToken)
    {
        var pendingItems = (await LoadAsync(cancellationToken)).ToList();
        if (pendingItems.Any(item => item.IdempotencyKey == pending.IdempotencyKey))
            return;
        pendingItems.Add(pending);
        await SaveAsync(pendingItems, cancellationToken);
    }

    public async Task RemoveAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var pendingItems = (await LoadAsync(cancellationToken))
            .Where(item => item.IdempotencyKey != idempotencyKey)
            .ToList();
        await SaveAsync(pendingItems, cancellationToken);
    }

    private async Task SaveAsync(
        IReadOnlyCollection<PendingReading> pendingItems,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporary,
            JsonSerializer.Serialize(pendingItems, JsonOptions),
            cancellationToken);
        File.Move(temporary, path, true);
    }
}
