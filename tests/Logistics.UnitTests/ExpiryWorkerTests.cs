using System;
using System.Threading.Tasks;
using FluentAssertions;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Services;
using Logistics.Infrastructure.Repositories;
using Logistics.Application.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Logistics.UnitTests
{
    public class ExpiryWorkerTests
    {
        private LogisticsDbContext CreateContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<LogisticsDbContext>()
                .UseSqlite(connection)
                .Options;
            var ctx = new LogisticsDbContext(options);
            return ctx;
        }

        private class TestClock : IClock
        {
            private readonly DateTime _now;
            public TestClock(DateTime now) { _now = now; }
            public Task<DateTime> GetUtcNowAsync() => Task.FromResult(_now);
        }

        [Fact]
        public async Task ProcessExpiredHoldsOnce_expires_and_releases_capacity_and_emits_outbox()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using var ctx = CreateContext(conn);
            // Disable foreign key enforcement before creating schema to avoid SQLite FK ordering quirks in-memory
            using (var cmd = conn.CreateCommand()) { cmd.CommandText = "PRAGMA foreign_keys = OFF;"; cmd.ExecuteNonQuery(); }
            ctx.Database.EnsureCreated();

            var voyageId = Guid.NewGuid();
            var holdId = Guid.NewGuid();

            // Seed data directly with SQL to avoid EF in-memory FK ordering issues
            var expiredAt = DateTime.UtcNow.AddSeconds(-5);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO voyage_capacity (voyage_id, total_capacity, held_capacity, confirmed_capacity, operational_status, version, created_at) VALUES ($voyageId, $total, $held, $confirmed, $status, 0, $created);";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "$voyageId"; p1.Value = voyageId; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "$total"; p2.Value = 10; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "$held"; p3.Value = 1; cmd.Parameters.Add(p3);
                var p4 = cmd.CreateParameter(); p4.ParameterName = "$confirmed"; p4.Value = 0; cmd.Parameters.Add(p4);
                var p5 = cmd.CreateParameter(); p5.ParameterName = "$status"; p5.Value = "Open"; cmd.Parameters.Add(p5);
                var p6 = cmd.CreateParameter(); p6.ParameterName = "$created"; p6.Value = DateTime.UtcNow; cmd.Parameters.Add(p6);
                cmd.ExecuteNonQuery();

                cmd.Parameters.Clear();
                cmd.CommandText = "INSERT INTO booking (booking_id, voyage_id, requested_capacity, state, version, created_at) VALUES ($b, $voyageId, $req, $state, 0, $created2);";
                var pb = cmd.CreateParameter(); pb.ParameterName = "$b"; pb.Value = Guid.NewGuid(); cmd.Parameters.Add(pb);
                var pv = cmd.CreateParameter(); pv.ParameterName = "$voyageId"; pv.Value = voyageId; cmd.Parameters.Add(pv);
                var pr = cmd.CreateParameter(); pr.ParameterName = "$req"; pr.Value = 1; cmd.Parameters.Add(pr);
                var ps = cmd.CreateParameter(); ps.ParameterName = "$state"; ps.Value = "Pending"; cmd.Parameters.Add(ps);
                var pc = cmd.CreateParameter(); pc.ParameterName = "$created2"; pc.Value = DateTime.UtcNow; cmd.Parameters.Add(pc);
                cmd.ExecuteNonQuery();

                cmd.Parameters.Clear();
                cmd.CommandText = "INSERT INTO capacity_hold (hold_id, booking_id, voyage_id, capacity_units, created_at, expires_at, status, version) VALUES ($hid, $bid, $vid, $units, $created3, $expires, $status2, 0);";
                var phid = cmd.CreateParameter(); phid.ParameterName = "$hid"; phid.Value = holdId; cmd.Parameters.Add(phid);
                var pbid = cmd.CreateParameter(); pbid.ParameterName = "$bid"; pbid.Value = pb.Value; cmd.Parameters.Add(pbid);
                var pvid = cmd.CreateParameter(); pvid.ParameterName = "$vid"; pvid.Value = voyageId; cmd.Parameters.Add(pvid);
                var pun = cmd.CreateParameter(); pun.ParameterName = "$units"; pun.Value = 1; cmd.Parameters.Add(pun);
                var pcreated3 = cmd.CreateParameter(); pcreated3.ParameterName = "$created3"; pcreated3.Value = DateTime.UtcNow.AddMinutes(-10); cmd.Parameters.Add(pcreated3);
                var pexpires = cmd.CreateParameter(); pexpires.ParameterName = "$expires"; pexpires.Value = expiredAt; cmd.Parameters.Add(pexpires);
                var pstatus2 = cmd.CreateParameter(); pstatus2.ParameterName = "$status2"; pstatus2.Value = "Active"; cmd.Parameters.Add(pstatus2);
                cmd.ExecuteNonQuery();
            }

            var repo = new VoyageCapacityRepository(ctx);
            var clock = new TestClock(DateTime.UtcNow);
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ExpiryWorker>();
            var worker = new ExpiryWorker(ctx, repo, clock, logger, TimeSpan.FromMilliseconds(10));

            var processed = await worker.ProcessExpiredHoldsOnceAsync();
            Assert.True(processed >= 1, "At least one expired hold should have been processed");

            var h = await ctx.CapacityHolds.FindAsync(holdId);
            Assert.Equal("Expired", h.Status);

            var v = await ctx.VoyageCapacities.FindAsync(voyageId);
            Assert.Equal(0, v.HeldCapacity);

            var outbox = await ctx.OutboxMessages.SingleAsync();
            Assert.Equal("CapacityHoldExpired", outbox.MessageType);
            Assert.False(outbox.Processed);
        }
    }
}
