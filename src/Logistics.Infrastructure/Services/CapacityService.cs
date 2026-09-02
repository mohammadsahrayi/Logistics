using Logistics.Application.Contracts;
using Logistics.Infrastructure.Persistence;
using System.Text.Json;

namespace Logistics.Infrastructure.Services
{
    public class CapacityService : ICapacityService
    {
        private readonly LogisticsDbContext _db;
        private readonly Repositories.VoyageCapacityRepository _voyageRepo;
        private readonly IClock _clock;

        public CapacityService(LogisticsDbContext db, Repositories.VoyageCapacityRepository voyageRepo, IClock clock)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _voyageRepo = voyageRepo ?? throw new ArgumentNullException(nameof(voyageRepo));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        private static string ComputeHash(object obj)
        {
            var json = JsonSerializer.Serialize(obj);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        public async Task<CreateHoldResult> CreateHoldAsync(Guid bookingId, Guid voyageId, int units, TimeSpan ttl, string? idempotencyKey = null)
        {
            if (units <= 0) return new CreateHoldResult(false, null, "units must be > 0");

            var requestFingerprint = ComputeHash(new { BookingId = bookingId, VoyageId = voyageId, Units = units, TtlMinutes = ttl.TotalMinutes });

            // Use transaction so capacity update, hold insert and outbox are atomic
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var dbNow = await _clock.GetUtcNowAsync();

                // Idempotency check / record
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                    if (existing != null)
                    {
                        // If hash differs, reject
                        if (!string.Equals(existing.RequestHash, requestFingerprint, StringComparison.OrdinalIgnoreCase))
                        {
                            await tx.CommitAsync();
                            return new CreateHoldResult(false, null, "Idempotency key reused with different payload");
                        }

                        // If completed, return stored result
                        if (existing.Status == "Completed" && !string.IsNullOrEmpty(existing.ResultJson))
                        {
                            var previous = JsonSerializer.Deserialize<CreateHoldResult>(existing.ResultJson);
                            await tx.CommitAsync();
                            return previous ?? new CreateHoldResult(false, null, "previous result missing");
                        }

                        // existing pending - treat as duplicate (could wait/retry in production)
                        await tx.CommitAsync();
                        return new CreateHoldResult(false, null, "Request is already in progress");
                    }

                    // Insert idempotency entry as pending
                    var idempotency = new IdempotencyEntryEntity
                    {
                        IdempotencyKey = idempotencyKey,
                        CreatedAt = dbNow,
                        RequestHash = requestFingerprint,
                        Status = "Pending"
                    };
                    _db.IdempotencyEntries.Add(idempotency);
                    await _db.SaveChangesAsync();
                }

                var booking = await _db.Bookings.FindAsync(bookingId);
                if (booking == null)
                {
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                        if (existing != null)
                        {
                            existing.Status = "Failed";
                            existing.ResponseStatusCode = 404;
                            existing.ResponseBody = "booking not found";
                            existing.CompletedAt = dbNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                    await tx.CommitAsync();
                    return new CreateHoldResult(false, null, "booking not found");
                }

                var voyage = await _db.VoyageCapacities.FindAsync(voyageId);
                if (voyage == null)
                {
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                        if (existing != null)
                        {
                            existing.Status = "Failed";
                            existing.ResponseStatusCode = 404;
                            existing.ResponseBody = "voyage not found";
                            existing.CompletedAt = dbNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                    await tx.CommitAsync();
                    return new CreateHoldResult(false, null, "voyage not found");
                }

                // Try reserve atomically using repository (same DbContext instance)
                var reserved = await _voyageRepo.TryReserveAtomic(voyageId, units);
                if (!reserved)
                {
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                        if (existing != null)
                        {
                            existing.Status = "Failed";
                            existing.ResponseStatusCode = 409;
                            existing.ResponseBody = "insufficient capacity or closed";
                            existing.CompletedAt = dbNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                    await tx.RollbackAsync();
                    return new CreateHoldResult(false, null, "insufficient capacity or closed");
                }

                var holdId = Guid.NewGuid();
                var holdEntity = new CapacityHoldEntity
                {
                    HoldId = holdId,
                    BookingId = bookingId,
                    VoyageId = voyageId,
                    CapacityUnits = units,
                    CreatedAt = dbNow,
                    ExpiresAt = dbNow.Add(ttl),
                    Status = "Active",
                    Version = 0
                };

                _db.CapacityHolds.Add(holdEntity);

                // Attach hold to booking
                booking.ActiveHoldId = holdId;
                booking.Version++;
                _db.Bookings.Update(booking);

                // Outbox message
                var evt = new { Type = "CapacityHoldCreated", HoldId = holdId, BookingId = bookingId, VoyageId = voyageId, Units = units, ExpiresAt = holdEntity.ExpiresAt };
                var outbox = new OutboxMessageEntity
                {
                    Id = Guid.NewGuid(),
                    MessageType = "CapacityHoldCreated",
                    Payload = JsonSerializer.Serialize(evt),
                    OccurredAt = DateTime.UtcNow,
                    Processed = false,
                    AttemptCount = 0
                };
                _db.OutboxMessages.Add(outbox);

                // Persist result in idempotency entry if present
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                    if (existing != null)
                    {
                        existing.Status = "Completed";
                        existing.ResponseStatusCode = 201;
                        existing.ResponseBody = JsonSerializer.Serialize(new { holdId = holdId });
                        existing.ResultJson = JsonSerializer.Serialize(new CreateHoldResult(true, holdId, null));
                        existing.CompletedAt = dbNow;
                        await _db.SaveChangesAsync();
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return new CreateHoldResult(true, holdId, null);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // ensure idempotency record reflects failure
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                    if (existing != null)
                    {
                        existing.Status = "Failed";
                        existing.ResponseStatusCode = 500;
                        existing.ResponseBody = ex.Message;
                        existing.CompletedAt = await _clock.GetUtcNowAsync();
                        await _db.SaveChangesAsync();
                    }
                }
                return new CreateHoldResult(false, null, ex.Message);
            }
        }

        public async Task<(bool Success, string? Reason)> ConfirmBookingAsync(Guid bookingId, Guid holdId, string? idempotencyKey = null)
        {
            var requestFingerprint = ComputeHash(new { BookingId = bookingId, HoldId = holdId });
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var dbNow = await _clock.GetUtcNowAsync();

                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                    if (existing != null)
                    {
                        if (!string.Equals(existing.RequestHash, requestFingerprint, StringComparison.OrdinalIgnoreCase))
                        {
                            await tx.CommitAsync();
                            return (false, "Idempotency key reused with different payload");
                        }

                        if (existing.Status == "Completed")
                        {
                            await tx.CommitAsync();
                            return (true, null);
                        }

                        // pending
                        await tx.CommitAsync();
                        return (false, "Request is already in progress");
                    }

                    // insert pending idempotency
                    _db.IdempotencyEntries.Add(new IdempotencyEntryEntity { IdempotencyKey = idempotencyKey, CreatedAt = dbNow, RequestHash = requestFingerprint, Status = "Pending" });
                    await _db.SaveChangesAsync();
                }

                var booking = await _db.Bookings.FindAsync(bookingId);
                if (booking == null)
                {
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                        if (existing != null)
                        {
                            existing.Status = "Failed";
                            existing.ResponseStatusCode = 404;
                            existing.ResponseBody = "booking not found";
                            existing.CompletedAt = dbNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                    await tx.CommitAsync();
                    return (false, "booking not found");
                }

                var hold = await _db.CapacityHolds.FindAsync(holdId);
                if (hold == null)
                {
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                        if (existing != null)
                        {
                            existing.Status = "Failed";
                            existing.ResponseStatusCode = 404;
                            existing.ResponseBody = "hold not found";
                            existing.CompletedAt = dbNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                    await tx.CommitAsync();
                    return (false, "hold not found");
                }

                if (hold.Status != "Active")
                {
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                        if (existing != null)
                        {
                            existing.Status = "Failed";
                            existing.ResponseStatusCode = 409;
                            existing.ResponseBody = "hold not active";
                            existing.CompletedAt = dbNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                    await tx.CommitAsync();
                    return (false, "hold not active");
                }

                if (dbNow >= hold.ExpiresAt)
                {
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                        if (existing != null)
                        {
                            existing.Status = "Failed";
                            existing.ResponseStatusCode = 409;
                            existing.ResponseBody = "hold expired";
                            existing.CompletedAt = dbNow;
                            await _db.SaveChangesAsync();
                        }
                    }
                    await tx.CommitAsync();
                    return (false, "hold expired");
                }

                // Mark hold confirmed
                hold.Status = "Confirmed";
                hold.Version++;
                hold.UpdatedAt = dbNow;
                _db.CapacityHolds.Update(hold);

                // Update booking state
                booking.State = "Confirmed";
                booking.Version++;
                _db.Bookings.Update(booking);

                // Convert held -> confirmed on voyage
                await _voyageRepo.ConfirmReserved(hold.VoyageId, hold.CapacityUnits);

                // Outbox
                var evt = new { Type = "BookingConfirmed", BookingId = bookingId, HoldId = holdId, VoyageId = hold.VoyageId, Units = hold.CapacityUnits };
                var outbox = new OutboxMessageEntity
                {
                    Id = Guid.NewGuid(),
                    MessageType = "BookingConfirmed",
                    Payload = JsonSerializer.Serialize(evt),
                    OccurredAt = DateTime.UtcNow,
                    Processed = false,
                    AttemptCount = 0
                };
                _db.OutboxMessages.Add(outbox);

                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                    if (existing != null)
                    {
                        existing.Status = "Completed";
                        existing.ResponseStatusCode = 200;
                        existing.ResponseBody = JsonSerializer.Serialize(new { Success = true });
                        existing.CompletedAt = dbNow;
                        existing.ResultJson = JsonSerializer.Serialize(new { Success = true });
                        await _db.SaveChangesAsync();
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                // ensure idempotency record reflects failure
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    var existing = await _db.IdempotencyEntries.FindAsync(idempotencyKey);
                    if (existing != null)
                    {
                        existing.Status = "Failed";
                        existing.ResponseStatusCode = 500;
                        existing.ResponseBody = ex.Message;
                        existing.CompletedAt = await _clock.GetUtcNowAsync();
                        await _db.SaveChangesAsync();
                    }
                }
                return (false, ex.Message);
            }
        }
    }
}
