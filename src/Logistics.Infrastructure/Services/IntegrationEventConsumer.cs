using System.Text.Json;
using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Infrastructure.Services
{
    public sealed class IntegrationEventConsumer
    {
        private readonly LogisticsDbContext _db;

        public IntegrationEventConsumer(LogisticsDbContext db)
        {
            _db = db;
        }

        public async Task<bool> ConsumeBookingConfirmedAsync(Guid messageId, string payload, CancellationToken ct = default)
        {
            var evt = JsonSerializer.Deserialize<BookingConfirmedEvent>(payload)
                ?? throw new InvalidOperationException("BookingConfirmed payload is invalid");

            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var inboxInserted = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO inbox_entry (message_id, received_at)
                    VALUES ({messageId}, {DateTime.UtcNow})
                    ON CONFLICT (message_id) DO NOTHING", ct);

            if (inboxInserted == 0)
            {
                await transaction.CommitAsync(ct);
                return false;
            }

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO booking_confirmation_projection
                        (booking_id, message_id, hold_id, voyage_id, capacity_units, received_at)
                    VALUES
                        ({evt.BookingId}, {messageId}, {evt.HoldId}, {evt.VoyageId}, {evt.Units}, {DateTime.UtcNow})
                    ON CONFLICT (booking_id) DO NOTHING", ct);

            await transaction.CommitAsync(ct);
            return true;
        }

        private sealed record BookingConfirmedEvent(Guid BookingId, Guid HoldId, Guid VoyageId, int Units);
    }
}