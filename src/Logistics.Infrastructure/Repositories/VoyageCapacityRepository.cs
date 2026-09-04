using Logistics.Application.Contracts;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Logistics.Infrastructure.Repositories
{
    public class VoyageCapacityRepository : IVoyageCapacityRepository
    {
        private readonly LogisticsDbContext _db;

        public VoyageCapacityRepository(LogisticsDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<bool> TryReserveAtomic(Guid voyageId, int units)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));

            // Atomic conditional update: only increase held_capacity when it doesn't violate invariant.
            // This uses a single UPDATE with a condition and checks affected rows.
            var sql = @"
UPDATE voyage_capacity
            SET held_capacity = held_capacity + @p0, version = version + 1
            WHERE voyage_id = @p1 AND operational_status = 'Open' AND (held_capacity + confirmed_capacity + @p0) <= total_capacity
";
            // Use parameterized form to avoid SQL injection
            var result = await _db.Database.ExecuteSqlRawAsync(sql, units, voyageId);
            return result == 1;
        }

        public async Task ConfirmReserved(Guid voyageId, int units)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));

            var sql = @"
                    UPDATE voyage_capacity
                                SET held_capacity = held_capacity - @p0, confirmed_capacity = confirmed_capacity + @p0, version = version + 1
                                WHERE voyage_id = @p1 AND (held_capacity - @p0) >= 0
                    ";
            var affected = await _db.Database.ExecuteSqlRawAsync(sql, units, voyageId);
            if (affected != 1) throw new InvalidOperationException("Failed to confirm reserved capacity (concurrent modification or insufficient held capacity)");
        }

        public async Task ReleaseReserved(Guid voyageId, int units)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));

            var sql = @"
UPDATE voyage_capacity
            SET held_capacity = held_capacity - @p0, version = version + 1
            WHERE voyage_id = @p1 AND (held_capacity - @p0) >= 0
";
            var affected = await _db.Database.ExecuteSqlRawAsync(sql, units, voyageId);
            if (affected != 1) throw new InvalidOperationException("Failed to release reserved capacity (concurrent modification or insufficient held capacity)");
        }
    }
}
