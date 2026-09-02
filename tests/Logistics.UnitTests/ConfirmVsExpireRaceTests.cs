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
    public class ConfirmVsExpireRaceTests
    {
        private const string EnvConn = "TEST_POSTGRES_CONN";

        private static string? GetConnString() => Environment.GetEnvironmentVariable(EnvConn);

        private LogisticsDbContext CreateContext(string connString)
        {
            var options = new DbContextOptionsBuilder<LogisticsDbContext>()
                .UseNpgsql(connString, b => b.EnableRetryOnFailure())
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
            {
                return; // no Postgres here
            }

            await using var masterConn = new NpgsqlConnection(connString);
            masterConn.Open();

            // Apply migrations if present
            var baseDir = Path.GetFullPath("src/Logistics.Infrastructure/Migrations");
            var files = new[] { "InitialCreate.sql", "AddConstraintsAndIndexes.sql", "AddVoyageCapacitySumCheck.sql" }
                .Select(n => Path.Combine(baseDir, n)).Where(File.Exists).ToList();
            foreach (var f in files)
            {
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
                ctx.Database.OpenConnection();
                var service = CreateService(ctx);
                createResult = await service.CreateHoldAsync(bookingId, voyageId, 1, TimeSpan.FromSeconds(1), "phase4-confirm-expire-create");
                createResult.Success.Should().BeTrue();
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
                    if ((DateTime.UtcNow - start).TotalSeconds > 10) break; // timeout
                    await Task.Delay(50);
                }
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            var confirmTask = Task.Run(async () =>
            {
                await using var ctx = CreateContext(connString);
                ctx.Database.OpenConnection();
                var service = CreateService(ctx);
                await tcs.Task; // wait for release
                var res = await service.ConfirmBookingAsync(bookingId, holdId, "phase4-confirm");
                return res;
            });

            var expireTask = Task.Run(async () =>
            {
                await using var ctx = CreateContext(connString);
                ctx.Database.OpenConnection();
                var repo = new VoyageCapacityRepository(ctx);
                var clock = new DbClockForTests(ctx.Database.GetDbConnection());

                await tcs.Task;

                // Expire logic: check DB time inside transaction and expire if eligible
                await using var tx = await ctx.Database.BeginTransactionAsync();
                var dbNow = await clock.GetUtcNowAsync();
                var h = await ctx.CapacityHolds.FindAsync(holdId);
                if (h == null)
                {
                    await tx.CommitAsync();
                    return false;
                }

                if (h.Status != "Active")
                {
                    await tx.CommitAsync();
                    return false;
                }

                if (dbNow < h.ExpiresAt)
                {
                    await tx.CommitAsync();
                    return false; // not yet expired
                }

                // mark expired and release capacity
                h.Status = "Expired";
                h.Version++;
                h.UpdatedAt = dbNow;
                ctx.CapacityHolds.Update(h);

                await repo.ReleaseReserved(h.VoyageId, h.CapacityUnits);

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            });

            // release both almost simultaneously
            tcs.SetResult(null);

            await Task.WhenAll(confirmTask, expireTask);

            var confirmRes = confirmTask.Result; // (bool, string?)
            var expireRes = expireTask.Result; // bool

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
    }
}
