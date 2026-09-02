using System;
using System.Threading.Tasks;
using FluentAssertions;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Services;
using Logistics.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Logistics.UnitTests
{
    public class IdempotencyTests
    {
        private LogisticsDbContext CreateContext()
        {
            // Use SQLite in-memory for tests so transactions are supported (unlike InMemory provider)
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<LogisticsDbContext>()
                .UseSqlite(connection)
                .Options;
            var ctx = new LogisticsDbContext(options);
            // ensure schema is created for each in-memory connection
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private ICapacityService CreateService(LogisticsDbContext ctx)
        {
            var repo = new Logistics.Infrastructure.Repositories.VoyageCapacityRepository(ctx);
            var clock = new TestClock();
            return new CapacityService(ctx, repo, clock);
        }

        private async Task SeedVoyageAndBooking(LogisticsDbContext ctx, Guid voyageId, Guid bookingId, int totalCapacity)
        {
            ctx.VoyageCapacities.Add(new VoyageCapacityEntity
            {
                VoyageId = voyageId,
                TotalCapacity = totalCapacity,
                HeldCapacity = 0,
                ConfirmedCapacity = 0,
                OperationalStatus = "Open",
                Version = 0,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Bookings.Add(new BookingEntity
            {
                BookingId = bookingId,
                VoyageId = voyageId,
                RequestedCapacity = 1,
                State = "Pending",
                Version = 0,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task First_request_with_new_idempotency_key_is_processed()
        {
            var ctx = CreateContext();
            var service = CreateService(ctx);
            var voyageId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            await SeedVoyageAndBooking(ctx, voyageId, bookingId, 5);

            var res = await service.CreateHoldAsync(bookingId, voyageId, 2, TimeSpan.FromMinutes(5), "key-1");

            res.Success.Should().BeTrue();
            res.HoldId.Should().NotBeNull();

            var entry = await ctx.IdempotencyEntries.FindAsync("key-1");
            entry.Should().NotBeNull();
            entry.Status.Should().Be("Completed");
        }

        [Fact]
        public async Task Retrying_same_key_same_payload_returns_stored_response()
        {
            var ctx = CreateContext();
            var service = CreateService(ctx);
            var voyageId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            await SeedVoyageAndBooking(ctx, voyageId, bookingId, 5);

            var res1 = await service.CreateHoldAsync(bookingId, voyageId, 2, TimeSpan.FromMinutes(5), "key-2");
            res1.Success.Should().BeTrue();

            var res2 = await service.CreateHoldAsync(bookingId, voyageId, 2, TimeSpan.FromMinutes(5), "key-2");
            res2.Success.Should().BeTrue();
            res2.HoldId.Should().Be(res1.HoldId);
        }

        [Fact]
        public async Task Reusing_same_key_with_different_payload_is_rejected()
        {
            var ctx = CreateContext();
            var service = CreateService(ctx);
            var voyageId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            await SeedVoyageAndBooking(ctx, voyageId, bookingId, 5);

            var res1 = await service.CreateHoldAsync(bookingId, voyageId, 2, TimeSpan.FromMinutes(5), "key-3");
            res1.Success.Should().BeTrue();

            var res2 = await service.CreateHoldAsync(bookingId, voyageId, 1, TimeSpan.FromMinutes(5), "key-3");
            res2.Success.Should().BeFalse();
            res2.Reason.Should().Contain("Idempotency key reused with different payload");
        }

        [Fact]
        public async Task Persisted_response_metadata_is_returned_on_successful_retry()
        {
            var ctx = CreateContext();
            var service = CreateService(ctx);
            var voyageId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            await SeedVoyageAndBooking(ctx, voyageId, bookingId, 5);

            var res1 = await service.CreateHoldAsync(bookingId, voyageId, 2, TimeSpan.FromMinutes(5), "key-4");
            res1.Success.Should().BeTrue();

            var entry = await ctx.IdempotencyEntries.FindAsync("key-4");
            entry.ResponseStatusCode.Should().Be(201);
            entry.ResponseBody.Should().NotBeNull();

            var res2 = await service.CreateHoldAsync(bookingId, voyageId, 2, TimeSpan.FromMinutes(5), "key-4");
            res2.Success.Should().BeTrue();
            res2.HoldId.Should().Be(res1.HoldId);
        }

        [Fact]
        public async Task Failed_processing_does_not_mark_completed()
        {
            var ctx = CreateContext();
            var service = CreateService(ctx);
            var voyageId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            // Do not seed booking to cause not found

            var res = await service.CreateHoldAsync(bookingId, voyageId, 2, TimeSpan.FromMinutes(5), "key-5");
            res.Success.Should().BeFalse();

            var entry = await ctx.IdempotencyEntries.FindAsync("key-5");
            entry.Should().NotBeNull();
            entry.Status.Should().Be("Failed");
            entry.CompletedAt.Should().NotBeNull();
        }

        private class TestClock : IClock
        {
            public Task<DateTime> GetUtcNowAsync() => Task.FromResult(DateTime.UtcNow);
        }
    }
}
