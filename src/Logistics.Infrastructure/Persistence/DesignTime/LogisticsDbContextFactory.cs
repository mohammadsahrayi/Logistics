using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace Logistics.Infrastructure.Persistence.DesignTime
{
    public class LogisticsDbContextFactory : IDesignTimeDbContextFactory<LogisticsDbContext>
    {
        public LogisticsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<LogisticsDbContext>();
            // Use a placeholder PostgreSQL connection string for design-time. This does not require a running DB to create migrations.
            var conn = "Host=localhost;Port=5432;Database=logistics_db;Username=logistics;Password=logistics_pwd";
            optionsBuilder.UseNpgsql(conn);
            return new LogisticsDbContext(optionsBuilder.Options);
        }
    }
}
