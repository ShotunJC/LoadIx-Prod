using System.Globalization;
using System.Net.Http.Headers;
using LoadIx.Application.Edge;
using LoadIx.EdgeSimulator;
using LoadIx.EdgeSimulator.Parsing;

var options = SimulatorOptions.Parse(args);
using var httpClient = new HttpClient
{
    BaseAddress = options.ApiBaseUrl,
    Timeout = TimeSpan.FromSeconds(30),
};
httpClient.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/json"));

var stateStore = new EdgeSimulatorStateStore(options.StateFile);
var pendingStore = new PendingSubmissionStore($"{options.StateFile}.pending");
var state = await stateStore.LoadAsync(CancellationToken.None);
var client = new EdgeSimulatorClient(httpClient);
if (state is null
    && options.DeviceId.HasValue
    && !string.IsNullOrWhiteSpace(options.BootstrapToken))
{
    Console.WriteLine("Registering the simulator with the one-time bootstrap token.");
    var proposedSecret = options.DeviceSecret ?? SimulatorSecretGenerator.Create();
    state = new EdgeSimulatorState(
        options.DeviceId.Value,
        proposedSecret,
        null,
        null,
        DateTimeOffset.UtcNow,
        options.BootstrapToken,
        proposedSecret,
        SimulatorSecretGenerator.CreateRequestKey("sim-bootstrap"),
        SimulatorSecretGenerator.CreateRequestKey("sim-bootstrap-nonce"));
    await stateStore.SaveAsync(state, CancellationToken.None);
}

if (state is null)
{
    Console.Error.WriteLine(
        "No simulator state exists. Supply --device-id and --bootstrap-token for the first run.");
    return 2;
}

if (!string.IsNullOrWhiteSpace(state.PendingBootstrapToken)
    && !string.IsNullOrWhiteSpace(state.PendingBootstrapSecret)
    && !string.IsNullOrWhiteSpace(state.PendingBootstrapIdempotencyKey)
    && !string.IsNullOrWhiteSpace(state.PendingBootstrapNonce))
{
    var issued = await client.BootstrapAsync(
        state.DeviceId,
        state.PendingBootstrapToken,
        state.PendingBootstrapSecret,
        state.PendingBootstrapIdempotencyKey,
        state.PendingBootstrapNonce,
        "loadix-edge-simulator/1.0",
        CancellationToken.None);
    state = state with
    {
        DeviceId = issued.DeviceId,
        DeviceSecret = state.PendingBootstrapSecret,
        PendingBootstrapToken = null,
        PendingBootstrapSecret = null,
        PendingBootstrapIdempotencyKey = null,
        PendingBootstrapNonce = null,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };
    await stateStore.SaveAsync(state, CancellationToken.None);
    Console.WriteLine("Bootstrap completed; local credential state was updated.");
}

client.SetCredentials(state.DeviceId, state.DeviceSecret);
if (!string.IsNullOrWhiteSpace(state.PendingReplacementSecret)
    && !string.IsNullOrWhiteSpace(state.PendingRotationIdempotencyKey)
    && !string.IsNullOrWhiteSpace(state.PendingRotationNonce)
    && !string.IsNullOrWhiteSpace(state.PendingConfirmationIdempotencyKey)
    && !string.IsNullOrWhiteSpace(state.PendingConfirmationNonce))
{
    await CompletePendingRotationAsync();
}

var configuration = await client.GetConfigurationAsync(CancellationToken.None);
state = state with
{
    WeighbridgeDeviceId = configuration.Weighbridges
        .FirstOrDefault(item => item.IsActive)?.Id,
    DeviceName = configuration.DeviceName,
    UpdatedAtUtc = DateTimeOffset.UtcNow,
};
await stateStore.SaveAsync(state, CancellationToken.None);
if (!state.WeighbridgeDeviceId.HasValue)
{
    Console.Error.WriteLine("The registered device has no active weighbridge configuration.");
    return 3;
}

var parser = new GenericLineWeightParser();
var pending = await pendingStore.LoadAsync(CancellationToken.None);
if (pending.Count > 0)
{
    Console.WriteLine($"Retrying {pending.Count} pending reading(s).");
    foreach (var item in pending)
    {
        if (await TrySendReadingAsync(item))
            await pendingStore.RemoveAsync(item.IdempotencyKey, CancellationToken.None);
    }
}

for (var index = 0; index < options.ReadingCount; index++)
{
    var disconnected = options.DisconnectEvery > 0
        && (index + 1) % options.DisconnectEvery == 0;
    var capturedAt = DateTimeOffset.UtcNow;
    var line = options.Line
        ?? $"WEIGHT|value={options.StartingValue + (index * options.StepValue):0.###}|unit=kg|stable={(index % 2 == 0).ToString().ToLowerInvariant()}|vehicle={options.VehicleRegistration}";
    var parsed = parser.Parse(line, capturedAt, $"sim-{Guid.NewGuid():N}");
    if (!parsed.IsSuccess)
    {
        Console.Error.WriteLine($"Parser rejected simulator input: {parsed.Code}");
        return 4;
    }

    var reading = new EdgeReadingModel(
        state.WeighbridgeDeviceId.Value,
        parsed.Value!.DisplayedValue,
        parsed.Value.Unit,
        parsed.Value.IsStable,
        parsed.Value.CapturedAtUtc,
        parsed.Value.VehicleRegistration,
        parsed.Value.SerialMessageMetadata,
        parsed.Value.ParserVersion,
        parsed.Value.CorrelationId);
    var pendingReading = new PendingReading($"sim-reading-{Guid.NewGuid():N}", reading);
    if (disconnected)
    {
        await pendingStore.AddAsync(pendingReading, CancellationToken.None);
        Console.WriteLine($"Simulated disconnect; queued reading {index + 1} locally.");
    }
    else
    {
        if (!await TrySendHeartbeatAsync())
        {
            await pendingStore.AddAsync(pendingReading, CancellationToken.None);
            Console.WriteLine("Heartbeat failed; queued reading locally.");
        }
        else if (await TrySendReadingAsync(pendingReading))
        {
            Console.WriteLine(
                $"Sent reading {index + 1}: {parsed.Value.DisplayedValue} {parsed.Value.Unit}.");
        }
        else
        {
            await pendingStore.AddAsync(pendingReading, CancellationToken.None);
            Console.WriteLine($"Reading {index + 1} deferred for retry.");
        }
    }

    if (options.RotateAfter > 0 && index + 1 == options.RotateAfter)
    {
        var replacementSecret = SimulatorSecretGenerator.Create();
        var oldSecret = state.DeviceSecret;
        state = state with
        {
            PreviousDeviceSecret = oldSecret,
            PendingReplacementSecret = replacementSecret,
            PendingRotationIdempotencyKey =
                SimulatorSecretGenerator.CreateRequestKey("sim-rotation"),
            PendingRotationNonce =
                SimulatorSecretGenerator.CreateRequestKey("sim-rotation-nonce"),
            PendingConfirmationIdempotencyKey =
                SimulatorSecretGenerator.CreateRequestKey("sim-rotation-confirm"),
            PendingConfirmationNonce =
                SimulatorSecretGenerator.CreateRequestKey("sim-rotation-confirm-nonce"),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        await stateStore.SaveAsync(state, CancellationToken.None);
        if (await CompletePendingRotationAsync())
            Console.WriteLine("Credential rotation confirmed; local state was updated.");
        else
            Console.Error.WriteLine("Credential rotation remains pending and will be retried.");
    }

    if (index + 1 < options.ReadingCount)
        await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds));
}

return 0;

async Task<bool> TrySendHeartbeatAsync()
{
    try
    {
        return await client.SendHeartbeatAsync(
            new EdgeHeartbeatModel(
                DateTimeOffset.UtcNow,
                "loadix-edge-simulator/1.0",
                "Running",
                new Dictionary<string, string>
                {
                    ["simulated"] = "true",
                    ["pending"] = (await pendingStore.LoadAsync(CancellationToken.None))
                        .Count.ToString(CultureInfo.InvariantCulture),
                }),
            CancellationToken.None);
    }
    catch (HttpRequestException)
    {
        return false;
    }
    catch (TaskCanceledException)
    {
        return false;
    }
}

async Task<bool> TrySendReadingAsync(PendingReading pendingReading)
{
    try
    {
        return await client.SendReadingAsync(pendingReading, CancellationToken.None);
    }
    catch (HttpRequestException)
    {
        return false;
    }
    catch (TaskCanceledException)
    {
        return false;
    }
}

async Task<bool> CompletePendingRotationAsync()
{
    if (string.IsNullOrWhiteSpace(state.PendingReplacementSecret)
        || string.IsNullOrWhiteSpace(state.PendingRotationIdempotencyKey)
        || string.IsNullOrWhiteSpace(state.PendingRotationNonce)
        || string.IsNullOrWhiteSpace(state.PendingConfirmationIdempotencyKey)
        || string.IsNullOrWhiteSpace(state.PendingConfirmationNonce))
    {
        return false;
    }

    var rotated = await client.RotateCredentialAsync(
        state.PendingReplacementSecret,
        state.PendingRotationIdempotencyKey,
        state.PendingRotationNonce,
        CancellationToken.None);
    if (rotated is null) return false;

    client.SetCredentials(state.DeviceId, state.PendingReplacementSecret);
    if (!await client.ConfirmCredentialRotationAsync(
            state.PendingConfirmationIdempotencyKey,
            state.PendingConfirmationNonce,
            CancellationToken.None))
    {
        if (!string.IsNullOrWhiteSpace(state.PreviousDeviceSecret))
        {
            state = state with
            {
                DeviceSecret = state.PreviousDeviceSecret,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            client.SetCredentials(state.DeviceId, state.DeviceSecret);
            await stateStore.SaveAsync(state, CancellationToken.None);
        }

        return false;
    }

    state = state with
    {
        DeviceSecret = state.PendingReplacementSecret,
        PreviousDeviceSecret = null,
        PendingReplacementSecret = null,
        PendingReplacementCredentialId = null,
        PendingRotationIdempotencyKey = null,
        PendingRotationNonce = null,
        PendingConfirmationIdempotencyKey = null,
        PendingConfirmationNonce = null,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };
    await stateStore.SaveAsync(state, CancellationToken.None);
    return true;
}
