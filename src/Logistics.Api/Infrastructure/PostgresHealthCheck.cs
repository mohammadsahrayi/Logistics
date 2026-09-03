using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Logistics.Api.Infrastructure
{
    public sealed class PostgresHealthCheck : IHealthCheck
    {
        private readonly LogisticsDbContext _db;

        public PostgresHealthCheck(LogisticsDbContext db)
        {
            _db = db;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _db.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy("PostgreSQL is reachable")
                    : HealthCheckResult.Unhealthy("PostgreSQL is not reachable");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL health check failed", ex);
            }
        }
    }
}