namespace LoadIx.Application.System;

public sealed record SystemInfoSnapshot(
    string ServiceName,
    string Version,
    string ApiVersion,
    DateTimeOffset ServerTimeUtc);
