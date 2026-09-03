using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Repositories;
using Logistics.Infrastructure.Services;
using Logistics.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Logistics.UnitTests
{
    [Collection("Postgres integration")]
    public class ConcurrencyIntegrationTests
    {
        private const string EnvConn = "TEST_POSTGRES_CONN";

        private static string? GetConnString()
        {
            return Environment.GetEnvironmentVariable(EnvConn);
        }

        private static void ApplySqlFilesIfNeeded(NpgsqlConnection conn)
        {
            var baseDir = FindRepositoryRoot().FullName is var root
                ? Path.Combine(root, "src", "Logistics.Infrastructure", "Migrations")
                : throw new DirectoryNotFoundException("Repository root was not found");
            var migrations = new[]
            {
                ("20260901185816_InitialCreate", "InitialCreate.sql"),
                ("20260901191623_AddConstraintsAndIndexes", "AddConstraintsAndIndexes.sql"),
                ("20260901191927_AddVoyageCapacitySumCheck", "AddVoyageCapacitySumCheck.sql"),
                ("20260903160050_AddBookingConfirmationProjection", "AddBookingConfirmationProjection.sql"),
                ("20260903163148_AddActiveHoldUniqueness", "AddActiveHoldUniqueness.sql")
            };

            using (var createHistoryCommand = conn.CreateCommand())
            {
                createHistoryCommand.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" character varying(150) NOT NULL, \"ProductVersion\" character varying(32) NOT NULL, CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\"))";
                createHistoryCommand.ExecuteNonQuery();
            }

            foreach (var (migrationId, fileName) in migrations)
            {
                using var historyCommand = conn.CreateCommand();
                historyCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id)";
                historyCommand.Parameters.AddWithValue("id", migrationId);
                if (Convert.ToBoolean(historyCommand.ExecuteScalar())) continue;

                var file = Path.Combine(baseDir, fileName);
                if (!File.Exists(file)) throw new FileNotFoundException("Migration script is missing", file);
                var sql = File.ReadAllText(file);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.ExecuteNonQuery();
            }
        }

        private static DirectoryInfo FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Logistics.slnx")))
                directory = directory.Parent;
            return directory ?? throw new DirectoryNotFoundException("Repository root was not found");
        }

        private LogisticsDbContext CreateContext(string connString)
        {
            var options = new DbContextOptionsBuilder<LogisticsDbContext>()
                .UseNpgsql(connString)
                .Options;
            return new LogisticsDbContext(options);
        }

        private ICapacityService CreateService(LogisticsDbContext ctx)
        {
            var repo = new VoyageCapacityRepository(ctx);
            var clock = new DbClockForTests(ctx.Database.GetDbConnection());
            return new CapacityService(ctx, repo, clock);
        }

        private class DbClockForTests : IClock
        {
            private readonly DbConnection _conn;
            public DbClockForTests(DbConnection conn) { _conn = conn; }
            public async Task<DateTime> GetUtcNowAsync()
            {
                await using var cmd = _conn.CreateCommand();
                cmd.CommandText = "SELECT NOW() AT TIME ZONE 'UTC'";
                var val = await cmd.ExecuteScalarAsync();
                return DateTime.SpecifyKind(Convert.ToDateTime(val), DateTimeKind.Utc);
            }
        }

        [Fact]
        public async Task FinalCapacity_overbooking_concurrent_create_hold_requests()
        {
            var connString = GetConnString();
            if (string.IsNullOrEmpty(connString))
            {
            throw new InvalidOperationException("TEST_POSTGRES_CONN must be configured; concurrency evidence cannot be skipped.");
            }

            // Prepare DB schema and seed data using a dedicated connection
            using var masterConn = new NpgsqlConnection(connString);
            masterConn.Open();

            // Apply migration SQL scripts if the tables are missing (idempotent-ish for test runs)
            ApplySqlFilesIfNeeded(masterConn);

            // Ensure a clean state for this test - use a unique voyage id
            var voyageId = Guid.NewGuid();
            var bookingA = Guid.NewGuid();
            var bookingB = Guid.NewGuid();

            // Create initial data directly via SQL for deterministic seeding
            using (var tx = masterConn.BeginTransaction())
            using (var cmd = masterConn.CreateCommand())
            {
                cmd.Transaction = tx;
                // Insert voyage with total_capacity 3, confirmed 1 -> available 2
                cmd.CommandText = @"
INSERT INTO voyage_capacity (voyage_id, total_capacity, held_capacity, confirmed_capacity, operational_status, version, created_at)
VALUES (@voyageId, 3, 0, 1, 'Open', 0, NOW() AT TIME ZONE 'UTC')
ON CONFLICT (voyage_id) DO UPDATE SET total_capacity = EXCLUDED.total_capacity;";
                cmd.Parameters.Add(new NpgsqlParameter("@voyageId", voyageId));
                cmd.ExecuteNonQuery();

                // Insert two bookings
                cmd.CommandText = @"
INSERT INTO booking (booking_id, voyage_id, requested_capacity, state, version, created_at)
VALUES (@b1, @voyageId, 2, 'Pending', 0, NOW() AT TIME ZONE 'UTC')
ON CONFLICT (booking_id) DO NOTHING;";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(new NpgsqlParameter("@b1", bookingA));
                cmd.Parameters.Add(new NpgsqlParameter("@voyageId", voyageId));
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
INSERT INTO booking (booking_id, voyage_id, requested_capacity, state, version, created_at)
VALUES (@b2, @voyageId, 1, 'Pending', 0, NOW() AT TIME ZONE 'UTC')
ON CONFLICT (booking_id) DO NOTHING;";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(new NpgsqlParameter("@b2", bookingB));
                cmd.Parameters.Add(new NpgsqlParameter("@voyageId", voyageId));
                cmd.ExecuteNonQuery();

                tx.Commit();
            }

            // Now perform concurrent CreateHold requests from separate DbContexts to simulate real concurrency
            var startGate = new Barrier(2);

            var taskA = Task.Run(async () =>
            {
                await using var ctx = CreateContext(connString);
                await ctx.Database.OpenConnectionAsync();
                var service = CreateService(ctx);
                startGate.SignalAndWait(TimeSpan.FromSeconds(10));
                return await service.CreateHoldAsync(bookingA, voyageId, 2, TimeSpan.FromMinutes(5), "phase4-key-a");
            });

            var taskB = Task.Run(async () =>
            {
                await using var ctx = CreateContext(connString);
                await ctx.Database.OpenConnectionAsync();
                var service = CreateService(ctx);
                startGate.SignalAndWait(TimeSpan.FromSeconds(10));
                return await service.CreateHoldAsync(bookingB, voyageId, 1, TimeSpan.FromMinutes(5), "phase4-key-b");
            });

            var results = await Task.WhenAll(taskA, taskB);
            var resA = results[0];
            var resB = results[1];

            // Read final voyage state
            using (var verifyCtx = CreateContext(connString))
            {
                var voyage = verifyCtx.VoyageCapacities.Single(v => v.VoyageId == voyageId);
                var total = voyage.TotalCapacity;
                var held = voyage.HeldCapacity;
                var confirmed = voyage.ConfirmedCapacity;

                // Invariant must hold
                Assert.True((held + confirmed) <= total, "held + confirmed must not exceed total");

                // The sum of successfully held units from both responses must not exceed the available 2 units
                var sumRequestedHeld = 0;
                if (resA.Success && resA.HoldId.HasValue)
                {
                    // fetch the hold to know units
                    var h = verifyCtx.CapacityHolds.SingleOrDefault(x => x.HoldId == resA.HoldId.Value);
                    h.Should().NotBeNull();
                    sumRequestedHeld += h.CapacityUnits;
                }
                if (resB.Success && resB.HoldId.HasValue)
                {
                    var h = verifyCtx.CapacityHolds.SingleOrDefault(x => x.HoldId == resB.HoldId.Value);
                    h.Should().NotBeNull();
                    sumRequestedHeld += h.CapacityUnits;
                }

                Assert.True(sumRequestedHeld <= 2, "Sum of held units by successful requests must not exceed available capacity (2)");

                // Exactly one of the two requests should have succeeded in reserving units that exceed or meet available capacity
                // Either A succeeded (2) and B failed, or B succeeded (1) and A failed; both succeeding would violate invariant
                var succeededCount = (resA.Success ? 1 : 0) + (resB.Success ? 1 : 0);
                succeededCount.Should().Be(1, $"the two requests together require 3 units but only 2 are available; A={resA.Reason}, B={resB.Reason}");
                held.Should().Be(resA.Success ? 2 : 1);

                // final check: no overbooking
                Assert.True((held + confirmed) <= total, "held + confirmed must not exceed total");
            }
        }
    }
}
