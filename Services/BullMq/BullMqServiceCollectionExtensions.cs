using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Reminders;
using StackExchange.Redis;

namespace Fakturio.Api.Services.BullMq;

public static class BullMqServiceCollectionExtensions
{
    /// <summary>
    /// Registers BullMQ reminder worker when enabled + Redis is configured;
    /// otherwise keeps the in-process <see cref="ReminderBackgroundService"/>.
    /// </summary>
    public static IServiceCollection AddFakturioBullMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BullMqOptions>(configuration.GetSection(BullMqOptions.SectionName));
        services.AddSingleton<IReminderJobRunner, ReminderJobRunner>();

        var bull = configuration.GetSection(BullMqOptions.SectionName).Get<BullMqOptions>()
            ?? new BullMqOptions();
        var redisConnection = ResolveRedisConnection(configuration, bull);
        var useBullMq = bull.Enabled && !string.IsNullOrWhiteSpace(redisConnection);

        if (useBullMq)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnection!));
            services.AddHostedService<ReminderBullMqHostedService>();
        }
        else
        {
            services.AddHostedService<ReminderBackgroundService>();
        }

        return services;
    }

    public static string? ResolveRedisConnection(IConfiguration configuration, BullMqOptions bull) =>
        FirstNonEmpty(
            bull.RedisConnection,
            configuration["Cache:RedisConnection"],
            configuration.GetConnectionString("Redis"));

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
