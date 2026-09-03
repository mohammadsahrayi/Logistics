using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Repositories;
using Logistics.Infrastructure.Services;
using Logistics.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data.Common;
using Xunit;

namespace Logistics.UnitTests
{
    [Collection("Postgres integration")]
    public class ConfirmVsExpireRaceTests
    {
        private const string EnvConn = "TEST_POSTGRES_CONN";

        private static string? GetConnString() => Environment.GetEnvironmentVariable(EnvConn);

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
        public async Task Confirm_vs_Expire_race_produces_single_terminal_outcome()
        {
            var connString = GetConnString();
            if (string.IsNullOrEmpty(connString))
                throw new InvalidOperationException("TEST_POSTGRES_CONN must be configured; concurrency evidence cannot be skipped.");

            await using var masterConn = new NpgsqlConnection(connString);
            masterConn.Open();

            // Apply migrations if present
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

            using (var createHistoryCommand = masterConn.CreateCommand())
            {
                createHistoryCommand.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" character varying(150) NOT NULL, \"ProductVersion\" character varying(32) NOT NULL, CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\"))";
                createHistoryCommand.ExecuteNonQuery();
            }

            foreach (var (migrationId, fileName) in migrations)
            {
                using var historyCommand = masterConn.CreateCommand();
                historyCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id)";
                historyCommand.Parameters.AddWithValue("id", migrationId);
                if (Convert.ToBoolean(historyCommand.ExecuteScalar())) continue;

                var f = Path.Combine(baseDir, fileName);
                if (!File.Exists(f)) throw new FileNotFoundException("Migration script is missing", f);
                var sql = File.ReadAllText(f);
                using var cmd = masterConn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.ExecuteNonQuery();
            }

            var voyageId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();

            // Seed voyage and booking
            using (var tx = masterConn.BeginTransaction())
            using (var cmd = masterConn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO voyage_capacity (voyage_id, total_capacity, held_capacity, confirmed_capacity, operational_status, version, created_at)
VALUES (@voyageId, 100, 0, 0, 'Open', 0, NOW() AT TIME ZONE 'UTC')
ON CONFLICT (voyage_id) DO UPDATE SET total_capacity = EXCLUDED.total_capacity;";
                cmd.Parameters.Add(new NpgsqlParameter("@voyageId", voyageId));
                cmd.ExecuteNonQuery();

                cmd.CommandText = @"
INSERT INTO booking (booking_id, voyage_id, requested_capacity, state, version, created_at)
VALUES (@b1, @voyageId, 1, 'Pending', 0, NOW() AT TIME ZONE 'UTC')
ON CONFLICT (booking_id) DO NOTHING;";
                cmd.Parameters.Clear();
                cmd.Parameters.Add(new NpgsqlParameter("@b1", bookingId));
                cmd.Parameters.Add(new NpgsqlParameter("@voyageId", voyageId));
                cmd.ExecuteNonQuery();

                tx.Commit();
            }

            // Create an active hold with TTL 1 second
            CreateHoldResult createResult;
            Guid holdId;
            DateTime expiresAt;
            await using (var ctx = CreateContext(connString))
            {
                await ctx.Database.OpenConnectionAsync();
                var service = CreateService(ctx);
                createResult = await service.CreateHoldAsync(bookingId, voyageId, 1, TimeSpan.FromSeconds(1), "phase4-confirm-expire-create");
                createResult.Success.Should().BeTrue(createResult.Reason);
                holdId = createResult.HoldId!.Value;

                var h = await ctx.CapacityHolds.FindAsync(holdId);
                h.Should().NotBeNull();
                expiresAt = h.ExpiresAt;
            }

            // Wait until DB time is at or very near the expiry moment
            await using (var waitCtx = CreateContext(connString))
            {
                waitCtx.Database.OpenConnection();
                var clock = new DbClockForTests(waitCtx.Database.GetDbConnection());

                var start = DateTime.UtcNow;
                while (true)
                {
                    var now = await clock.GetUtcNowAsync();
                    if (now >= expiresAt) break;
                    if ((DateTime.UtcNow - start).TotalSeconds > 10)
                        throw new TimeoutException("Database time did not reach the hold expiry before the test timeout.");
                    await Task.Delay(50);
                }
            }

            var startGate = new Barrier(2);

            var confirmTask = Task.Run(async () =>
            {
                await using var ctx = CreateContext(connString);
                await ctx.Database.OpenConnectionAsync();
                var service = CreateService(ctx);
                startGate.SignalAndWait(TimeSpan.FromSeconds(10));
                var res = await service.ConfirmBookingAsync(bookingId, holdId, "phase4-confirm");
                return res;
            });

            var expireTask = Task.Run(async () =>
            {
                await using var ctx = CreateContext(connString);
                await ctx.Database.OpenConnectionAsync();
                var clock = new DbClockForTests(ctx.Database.GetDbConnection());
                var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ExpiryWorker>();
                var worker = new ExpiryWorker(ctx, new VoyageCapacityRepository(ctx), clock, logger);

                startGate.SignalAndWait(TimeSpan.FromSeconds(10));
                return await worker.ProcessExpiredHoldsOnceAsync();
            });

            await Task.WhenAll(confirmTask, expireTask);

            var confirmRes = await confirmTask;
            var expireRes = await expireTask;

            // Verify final persisted state
            await using (var verifyCtx = CreateContext(connString))
            {
                verifyCtx.Database.OpenConnection();
                var h = await verifyCtx.CapacityHolds.FindAsync(holdId);
                h.Should().NotBeNull();

                var voyage = verifyCtx.VoyageCapacities.Single(v => v.VoyageId == voyageId);

                // Exactly one terminal state: either Confirmed or Expired (and not both)
                var isConfirmed = h.Status == "Confirmed";
                var isExpired = h.Status == "Expired";
                (isConfirmed || isExpired).Should().BeTrue();
                (isConfirmed ^ isExpired).Should().BeTrue();
                ((confirmRes.Success ? 1 : 0) + (expireRes > 0 ? 1 : 0)).Should().Be(1);

                // If confirmed, confirmed_capacity increased by 1 and held decreased accordingly
                if (isConfirmed)
                {
                    Assert.True(voyage.ConfirmedCapacity >= 1, "confirmed capacity should be at least 1 when confirmed");
                }

                // If expired, held should be released (held should be 0)
                if (isExpired)
                {
                    Assert.True(voyage.HeldCapacity == 0, "held capacity should be 0 when expired");
                }

                // Invariant
                Assert.True((voyage.HeldCapacity + voyage.ConfirmedCapacity) <= voyage.TotalCapacity, "held + confirmed must not exceed total capacity");
            }
        }

        private static DirectoryInfo FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Logistics.slnx")))
                directory = directory.Parent;
            return directory ?? throw new DirectoryNotFoundException("Repository root was not found");
        }
    }
}
