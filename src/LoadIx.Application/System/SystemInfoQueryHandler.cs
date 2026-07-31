namespace LoadIx.Application.System;

public sealed class SystemInfoQueryHandler(
    TimeProvider timeProvider,
    SystemInfoDescriptor descriptor) : ISystemInfoQueryHandler
{
    public ValueTask<SystemInfoSnapshot> HandleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new SystemInfoSnapshot(
            descriptor.ServiceName,
            descriptor.Version,
            descriptor.ApiVersion,
            timeProvider.GetUtcNow());

        return ValueTask.FromResult(result);
    }
}
