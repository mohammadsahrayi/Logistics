using FluentAssertions;
using Logistics.Application.Contracts;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Repositories;
using Logistics.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Logistics.UnitTests
{
    public class CapacityHoldQueryTests
    {
        [Fact]
        public async Task GetCapacityHold_returns_latest_hold_for_booking()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LogisticsDbContext>().UseSqlite(connection).Options;
            await using var context = new LogisticsDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var bookingId = Guid.NewGuid();
            var voyageId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            context.VoyageCapacities.Add(new VoyageCapacityEntity
            {
                VoyageId = voyageId,
                TotalCapacity = 10,
                OperationalStatus = "Open",
                CreatedAt = createdAt
            });
            context.Bookings.Add(new BookingEntity
            {
                BookingId = bookingId,
                VoyageId = voyageId,
                RequestedCapacity = 2,
                State = "Pending",
                CreatedAt = createdAt
            });
            context.CapacityHolds.AddRange(
                new CapacityHoldEntity { HoldId = Guid.NewGuid(), BookingId = bookingId, VoyageId = voyageId, CapacityUnits = 1, CreatedAt = createdAt.AddMinutes(-1), ExpiresAt = createdAt.AddMinutes(4), Status = "Expired", Version = 1 },
                new CapacityHoldEntity { HoldId = Guid.NewGuid(), BookingId = bookingId, VoyageId = voyageId, CapacityUnits = 2, CreatedAt = createdAt, ExpiresAt = createdAt.AddMinutes(5), Status = "Active", Version = 0 });
            await context.SaveChangesAsync();

            var service = new CapacityService(context, new VoyageCapacityRepository(context), new TestClock());
            var result = await service.GetCapacityHoldAsync(bookingId);

            result.Should().NotBeNull();
            result!.CapacityUnits.Should().Be(2);
            result.Status.Should().Be("Active");
        }

        [Fact]
        public async Task GetCapacityHold_returns_null_for_unknown_booking()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LogisticsDbContext>().UseSqlite(connection).Options;
            await using var context = new LogisticsDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var service = new CapacityService(context, new VoyageCapacityRepository(context), new TestClock());

            (await service.GetCapacityHoldAsync(Guid.NewGuid())).Should().BeNull();
        }

        [Fact]
        public async Task GetVoyageCapacity_returns_available_capacity()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<LogisticsDbContext>().UseSqlite(connection).Options;
            await using var context = new LogisticsDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var voyageId = Guid.NewGuid();
            context.VoyageCapacities.Add(new VoyageCapacityEntity
            {
                VoyageId = voyageId,
                TotalCapacity = 10,
                HeldCapacity = 3,
                ConfirmedCapacity = 4,
                OperationalStatus = "Open",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var service = new CapacityService(context, new VoyageCapacityRepository(context), new TestClock());
            var result = await service.GetVoyageCapacityAsync(voyageId);

            result.Should().NotBeNull();
            result!.AvailableCapacity.Should().Be(3);
            result.HeldCapacity.Should().Be(3);
            result.ConfirmedCapacity.Should().Be(4);
        }

        private sealed class TestClock : IClock
        {
            public Task<DateTime> GetUtcNowAsync() => Task.FromResult(DateTime.UtcNow);
        }
    }
}