namespace LoadIx.Contracts.System;

public sealed record SystemInfoResponse(
    string ServiceName,
    string Version,
    string ApiVersion,
    DateTimeOffset ServerTimeUtc);
