namespace LoadIx.Application.System;

public sealed record SystemInfoDescriptor(
    string ServiceName,
    string Version,
    string ApiVersion);
