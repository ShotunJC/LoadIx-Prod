namespace LoadIx.Application.System;

public interface ISystemInfoQueryHandler
{
    ValueTask<SystemInfoSnapshot> HandleAsync(CancellationToken cancellationToken);
}
