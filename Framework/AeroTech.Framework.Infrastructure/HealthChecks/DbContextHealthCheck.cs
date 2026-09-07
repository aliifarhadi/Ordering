using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AeroTech.Framework.Infrastructure.HealthChecks
{
    /// <summary>
    /// Readiness check that verifies the application can open a connection to the
    /// database backing <typeparamref name="TContext"/>.
    /// </summary>
    public sealed class DbContextHealthCheck<TContext> : IHealthCheck
        where TContext : DbContext
    {
        private readonly TContext _context;

        public DbContextHealthCheck(TContext context) => _context = context;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("Database is not reachable.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database connectivity check failed.", ex);
            }
        }
    }
}
