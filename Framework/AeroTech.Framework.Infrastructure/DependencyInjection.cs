using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Framework.Infrastructure.HealthChecks;
using AeroTech.Framework.Infrastructure.Options;
using AeroTech.Framework.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace AeroTech.Framework.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddFrameworkInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IClock, UtcClock>();

            var idGenerator = configuration.GetSection("IdGenerator").Get<IdGeneratorOptions>() ?? new IdGeneratorOptions();

            if (idGenerator.GeneratorId is not { } generatorId)
                throw new InvalidOperationException("'IdGenerator:GeneratorId' must be configured with a value unique to this instance.");

            if (generatorId < 0 || generatorId > IdGeneratorOptions.MaxGeneratorId)
                throw new InvalidOperationException($"'IdGenerator:GeneratorId' must be between 0 and {IdGeneratorOptions.MaxGeneratorId}.");

            services.AddSingleton<IIdGenerator>(new SnowflakeIdGenerator(generatorId));

            var redis = configuration.GetSection("Redis").Get<RedisOptions>() ?? new RedisOptions();
            ArgumentException.ThrowIfNullOrWhiteSpace(redis.Host, "Redis:Host");

            var multiplexer = ConnectionMultiplexer.Connect($"{redis.Host},abortConnect=false");
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            var redLockFactory = RedLockFactory.Create(new List<RedLockMultiplexer> { new RedLockMultiplexer(multiplexer) });
            services.AddSingleton<IDistributedLock>(new RedLockDistributedLock(redLockFactory, redis.Prefix));

            services.AddHealthChecks().AddRedisReadinessCheck();

            return services;
        }
    }
}
