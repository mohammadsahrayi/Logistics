using Logistics.Application.Contracts;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Logistics.Infrastructure.Services
{
    public class ExpiryWorker : BackgroundService
    {
        private readonly LogisticsDbContext _db;
        private readonly VoyageCapacityRepository _voyageRepo;
        private readonly IClock _clock;
        private readonly ILogger<ExpiryWorker> _logger;
        private readonly TimeSpan _pollInterval;

        public ExpiryWorker(LogisticsDbContext db, VoyageCapacityRepository voyageRepo, IClock clock, ILogger<ExpiryWorker> logger, TimeSpan? pollInterval = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _voyageRepo = voyageRepo ?? throw new ArgumentNullException(nameof(voyageRepo));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExpiryWorker started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredHoldsOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing expired holds");
                }

                try
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                }
                catch (TaskCanceledException) { }
            }
            _logger.LogInformation("ExpiryWorker stopped");
        }

        public async Task<int> ProcessExpiredHoldsOnceAsync(CancellationToken ct = default)
        {
            // Find candidate holds that are Active and whose expires_at <= clock (approx). We'll refine inside TX.
            var candidates = await _db.CapacityHolds
                .Where(h => h.Status == "Active")
                .OrderBy(h => h.ExpiresAt)
                .Take(50)
                .ToListAsync(ct);

            var processed = 0;

            foreach (var candidate in candidates)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // Use a transaction so expiry check and release are atomic
                    await using var tx = await _db.Database.BeginTransactionAsync(ct);

                    var dbNow = await _clock.GetUtcNowAsync();

                    // re-load inside transaction to ensure fresh state/locks
                    var h = await _db.CapacityHolds.FindAsync(new object[] { candidate.HoldId }, ct);
                    if (h == null)
                    {
                        await tx.CommitAsync(ct);
                        continue;
                    }

                    if (h.Status != "Active")
                    {
                        await tx.CommitAsync(ct);
                        continue;
                    }

                    if (dbNow < h.ExpiresAt)
                    {
                        await tx.CommitAsync(ct);
                        continue;
                    }

                    // Update status in the same transaction and clear the change tracker before re-reading this row
                    // so the caller sees the fresh persisted state rather than a stale tracked entity.
                    var updateSql = @"UPDATE capacity_hold SET status = @p0, version = version + 1, updated_at = @p1 WHERE hold_id = @p2 AND status = 'Active' AND (expires_at <= @p3)";
                    var updated = await _db.Database.ExecuteSqlRawAsync(updateSql, "Expired", dbNow, h.HoldId, dbNow);
                    if (updated != 1)
                    {
                        await tx.CommitAsync(ct);
                        continue; // someone else raced and changed status
                    }

                    _db.ChangeTracker.Clear();
                    var refreshedHold = await _db.CapacityHolds.AsNoTracking().SingleAsync(x => x.HoldId == h.HoldId, ct);
                    if (refreshedHold.Status != "Expired")
                    {
                        await tx.CommitAsync(ct);
                        continue;
                    }

                    // release reserved on voyage
                    await _voyageRepo.ReleaseReserved(h.VoyageId, h.CapacityUnits);

                    // Insert outbox via raw SQL
                    var evt = System.Text.Json.JsonSerializer.Serialize(new { Type = "CapacityHoldExpired", HoldId = h.HoldId, BookingId = h.BookingId, VoyageId = h.VoyageId, Units = h.CapacityUnits });
                    var insertOutbox = @"INSERT INTO outbox_message (id, message_type, payload, occurred_at, processed, attempt_count) VALUES (@p0, @p1, @p2, @p3, false, 0)";
                    await _db.Database.ExecuteSqlRawAsync(insertOutbox, Guid.NewGuid(), "CapacityHoldExpired", evt, DateTime.UtcNow);

                    await tx.CommitAsync(ct);

                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process expiry for hold {HoldId}", candidate.HoldId);
                    // swallow and continue
                }
            }

            return processed;
        }
    }
}
