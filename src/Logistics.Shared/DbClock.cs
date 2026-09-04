using Microsoft.EntityFrameworkCore;

namespace Logistics.Shared
{
    /// <summary>
    /// Database-sourced clock for consistency in transactions
    /// </summary>
    public class DbClock : IClock
    {
        private readonly DbContext _db;

        public DbClock(DbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public Task<DateTime> GetUtcNowAsync()
        {
            return _db.Database.SqlQueryRaw<DateTime>("SELECT (NOW() AT TIME ZONE 'UTC')::timestamp").FirstAsync();
        }
    }
}
