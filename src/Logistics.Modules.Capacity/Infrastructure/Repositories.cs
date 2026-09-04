using Logistics.Modules.Capacity.Domain;
using Logistics.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Logistics.Modules.Capacity.Infrastructure
{
    /// <summary>
    /// Repository for VoyageCapacity aggregate - handles atomic capacity operations
    /// </summary>
    public class VoyageCapacityRepository : IVoyageCapacityRepository
    {
        private readonly DbContext _db;

        public VoyageCapacityRepository(DbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Atomically reserve capacity if available. Uses database-level consistency.
        /// </summary>
        public async Task<bool> TryReserveAtomic(Guid voyageId, int units)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));

            var sql = @"
                        UPDATE voyage_capacity
                        SET held_capacity = held_capacity + @p0, version = version + 1
                        WHERE voyage_id = @p1 AND operational_status = 'Open' AND (held_capacity + confirmed_capacity + @p0) <= total_capacity
                        ";
            var result = await _db.Database.ExecuteSqlRawAsync(sql, units, voyageId);
            return result == 1;
        }

        /// <summary>
        /// Confirm reserved capacity by moving from held to confirmed
        /// </summary>
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

        /// <summary>
        /// Release reserved capacity back to available
        /// </summary>
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

        public async Task<VoyageCapacity?> GetByIdAsync(Guid voyageId)
        {
            var entity = await _db.Set<VoyageCapacityEntity>().FindAsync(voyageId);
            if (entity == null) return null;

            var capacity = new VoyageCapacity(
                entity.VoyageId,
                entity.TotalCapacity);

            capacity.RestoreState(
                entity.HeldCapacity,
                entity.ConfirmedCapacity,
                entity.OperationalStatus,
                entity.Version);

            return capacity;
        }

        public Task SaveAsync(VoyageCapacity voyage)
        {
            var entity = new VoyageCapacityEntity
            {
                VoyageId = voyage.VoyageId,
                TotalCapacity = voyage.TotalCapacity,
                HeldCapacity = voyage.HeldCapacity,
                ConfirmedCapacity = voyage.ConfirmedCapacity,
                OperationalStatus = voyage.OperationalStatus,
                Version = voyage.Version,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Set<VoyageCapacityEntity>().Add(entity);
            return _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Repository for CapacityHold aggregate
    /// </summary>
    public class CapacityHoldRepository : ICapacityHoldRepository
    {
        private readonly DbContext _db;

        public CapacityHoldRepository(DbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<CapacityHold?> GetByIdAsync(Guid holdId)
        {
            var entity = await _db
           .Set<CapacityHoldEntity>()
           .FindAsync(holdId);

            if (entity == null)
                return null;

            var hold = new CapacityHold(
                entity.HoldId,
                entity.BookingId,
                entity.VoyageId,
                entity.CapacityUnits,
                entity.CreatedAt,
                entity.ExpiresAt);

            hold.RestoreState(
                entity.Status,
                entity.Version);

            return hold;
        }

        public Task SaveAsync(CapacityHold hold)
        {
            var entity = new CapacityHoldEntity
            {
                HoldId = hold.HoldId,
                BookingId = hold.BookingId,
                VoyageId = hold.VoyageId,
                CapacityUnits = hold.CapacityUnits,
                CreatedAt = hold.CreatedAt,
                ExpiresAt = hold.ExpiresAt,
                Status = hold.Status,
                Version = hold.Version,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Set<CapacityHoldEntity>().Add(entity);
            return _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<CapacityHold>> GetExpiredHoldsAsync(DateTime now)
        {
            var entities = await _db.Set<CapacityHoldEntity>()
                .Where(h => h.Status == "Active" && h.ExpiresAt <= now)
                .OrderBy(h => h.ExpiresAt)
                .Take(50)
                .ToListAsync();

            return entities.Select(e =>
            {
                var hold = new CapacityHold(
                    e.HoldId,
                    e.BookingId,
                    e.VoyageId,
                    e.CapacityUnits,
                    e.CreatedAt,
                    e.ExpiresAt);

                hold.RestoreState(e.Status, e.Version);

                return hold;
            }).ToList();
        }
    }
}
