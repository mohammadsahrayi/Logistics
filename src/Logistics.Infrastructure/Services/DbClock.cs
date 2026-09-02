using Logistics.Application.Contracts;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Logistics.Infrastructure.Services
{
    public class DbClock : IClock
    {
        private readonly LogisticsDbContext _db;

        public DbClock(LogisticsDbContext db)
        {
            _db = db;
        }

        public async Task<DateTime> GetUtcNowAsync()
        {
            var conn = _db.Database.GetDbConnection();
            // Do not dispose the connection obtained from DbContext; it is managed by the context.
            if (conn.State == System.Data.ConnectionState.Closed)
                await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT NOW() AT TIME ZONE 'UTC'";
            var result = await cmd.ExecuteScalarAsync();
            if (result is DateTime dt)
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            // fallback
            return DateTime.UtcNow;
        }
    }
}
