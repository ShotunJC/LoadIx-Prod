using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace LoadIx.Api.Configuration;

public static class SignalRRedisConfiguration
{
    public static ISignalRServerBuilder AddLoadIxRedisBackplane(
        this ISignalRServerBuilder signalR,
        DependencyOptions dependencies)
    {
        ArgumentNullException.ThrowIfNull(signalR);
        ArgumentNullException.ThrowIfNull(dependencies);

        var configuration = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            Password = string.IsNullOrWhiteSpace(dependencies.RedisPassword)
                ? null
                : dependencies.RedisPassword,
            Ssl = dependencies.RedisUseSsl,
            SslHost = string.IsNullOrWhiteSpace(dependencies.RedisServerName)
                ? dependencies.RedisHost
                : dependencies.RedisServerName,
        };
        configuration.EndPoints.Add(
            dependencies.RedisHost,
            dependencies.RedisPort);

        return signalR.AddStackExchangeRedis(options =>
        {
            options.Configuration = configuration;
            options.Configuration.ChannelPrefix = RedisChannel.Literal("LoadIx");
        });
    }
}
