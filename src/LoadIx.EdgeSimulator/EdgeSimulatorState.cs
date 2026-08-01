using System.Text.Json;

namespace LoadIx.EdgeSimulator;

public sealed record EdgeSimulatorState(
    Guid DeviceId,
    string DeviceSecret,
    Guid? WeighbridgeDeviceId,
    string? DeviceName,
    DateTimeOffset UpdatedAtUtc);

public sealed class EdgeSimulatorStateStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };

    public async Task<EdgeSimulatorState?> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<EdgeSimulatorState>(
            stream,
            JsonOptions,
            cancellationToken);
    }

    public async Task SaveAsync(
        EdgeSimulatorState state,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporary,
            JsonSerializer.Serialize(state, JsonOptions),
            cancellationToken);
        File.Move(temporary, path, true);
    }
}
