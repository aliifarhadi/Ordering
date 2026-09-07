using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AeroTech.Framework.Infrastructure.HealthChecks
{
    public static class HealthCheckBuilderExtensions
    {
        /// <summary>Tag for checks that must pass before the pod is sent traffic (Kubernetes readiness probe).</summary>
        public const string ReadyTag = "ready";

        /// <summary>Tag for checks that indicate the process itself is alive (Kubernetes liveness probe).</summary>
        public const string LiveTag = "live";

        /// <summary>
        /// Registers a readiness check that confirms <typeparamref name="TContext"/> can reach its database.
        /// </summary>
        public static IHealthChecksBuilder AddDbContextReadinessCheck<TContext>(
            this IHealthChecksBuilder builder, string name)
            where TContext : DbContext =>
            builder.AddCheck<DbContextHealthCheck<TContext>>(
                name, HealthStatus.Unhealthy, new[] { ReadyTag });

        /// <summary>
        /// Registers a readiness check that confirms Redis is reachable.
        /// </summary>
        public static IHealthChecksBuilder AddRedisReadinessCheck(
            this IHealthChecksBuilder builder, string name = "redis") =>
            builder.AddCheck<RedisHealthCheck>(
                name, HealthStatus.Unhealthy, new[] { ReadyTag });
    }
}
