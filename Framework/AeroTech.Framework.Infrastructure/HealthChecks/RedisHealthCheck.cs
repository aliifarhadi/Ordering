using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AeroTech.Framework.Infrastructure.HealthChecks
{
    /// <summary>
    /// Readiness check that pings the Redis instance used for distributed locking.
    /// </summary>
    public sealed class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _multiplexer;

        public RedisHealthCheck(IConnectionMultiplexer multiplexer) => _multiplexer = multiplexer;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_multiplexer.IsConnected)
                    return HealthCheckResult.Unhealthy("Redis is not connected.");

                await _multiplexer.GetDatabase().PingAsync();
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis connectivity check failed.", ex);
            }
        }
    }
}
