namespace LoadIx.EdgeSimulator;

public sealed record SimulatorOptions(
    Uri ApiBaseUrl,
    Guid? DeviceId,
    string? BootstrapToken,
    string? DeviceSecret,
    string StateFile,
    int ReadingCount,
    int IntervalSeconds,
    int DisconnectEvery,
    int RotateAfter,
    decimal StartingValue,
    decimal StepValue,
    string VehicleRegistration,
    string? Line)
{
    public static SimulatorOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[index][2..];
            var value = index + 1 < args.Length
                && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++index]
                : "true";
            values[key] = value;
        }

        var baseUrl = Get(values, "api-base-url", "LOADIX_EDGE_API_URL")
            ?? "http://localhost:8080";
        return new SimulatorOptions(
            new Uri(baseUrl, UriKind.Absolute),
            ParseGuid(Get(values, "device-id", "LOADIX_EDGE_DEVICE_ID")),
            Get(values, "bootstrap-token", "LOADIX_EDGE_BOOTSTRAP_TOKEN"),
            Get(values, "device-secret", "LOADIX_EDGE_DEVICE_SECRET"),
            Get(values, "state-file", "LOADIX_EDGE_STATE_FILE")
                ?? Path.Combine(AppContext.BaseDirectory, "edge-simulator-state.json"),
            ParseInt(values, "count", "LOADIX_EDGE_COUNT", 10, 1, 100_000),
            ParseInt(values, "interval-seconds", "LOADIX_EDGE_INTERVAL_SECONDS", 5, 1, 3600),
            ParseInt(values, "disconnect-every", "LOADIX_EDGE_DISCONNECT_EVERY", 0, 0, 100_000),
            ParseInt(values, "rotate-after", "LOADIX_EDGE_ROTATE_AFTER", 0, 0, 100_000),
            ParseDecimal(values, "starting-value", "LOADIX_EDGE_STARTING_VALUE", 12_345m),
            ParseDecimal(values, "step-value", "LOADIX_EDGE_STEP_VALUE", 25m),
            Get(values, "vehicle", "LOADIX_EDGE_VEHICLE") ?? "SIM-001",
            Get(values, "line", "LOADIX_EDGE_LINE"));
    }

    private static string? Get(
        IReadOnlyDictionary<string, string> values,
        string argument,
        string environmentVariable) =>
        values.GetValueOrDefault(argument)
        ?? Environment.GetEnvironmentVariable(environmentVariable);

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;

    private static int ParseInt(
        IReadOnlyDictionary<string, string> values,
        string argument,
        string environmentVariable,
        int fallback,
        int minimum,
        int maximum)
    {
        var value = Get(values, argument, environmentVariable);
        return int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;
    }

    private static decimal ParseDecimal(
        IReadOnlyDictionary<string, string> values,
        string argument,
        string environmentVariable,
        decimal fallback)
    {
        var value = Get(values, argument, environmentVariable);
        return decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;
    }
}
