using System.Text.Json;
using FluentAssertions;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Logistics.UnitTests
{
    public class IntegrationEventConsumerTests
    {
        [Fact]
        public async Task Duplicate_booking_confirmed_delivery_has_one_downstream_effect()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<LogisticsDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var context = new LogisticsDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var messageId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new
            {
                BookingId = bookingId,
                HoldId = Guid.NewGuid(),
                VoyageId = Guid.NewGuid(),
                Units = 2
            });
            var consumer = new IntegrationEventConsumer(context);

            var first = await consumer.ConsumeBookingConfirmedAsync(messageId, payload);
            var duplicate = await consumer.ConsumeBookingConfirmedAsync(messageId, payload);

            first.Should().BeTrue();
            duplicate.Should().BeFalse();
            (await context.InboxEntries.CountAsync()).Should().Be(1);
            (await context.BookingConfirmationProjections.CountAsync()).Should().Be(1);
            (await context.BookingConfirmationProjections.SingleAsync()).BookingId.Should().Be(bookingId);
        }
    }
}