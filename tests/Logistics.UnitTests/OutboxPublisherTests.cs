using System;
using System.Threading.Tasks;
using FluentAssertions;
using Logistics.Infrastructure.Persistence;
using Logistics.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Logistics.UnitTests
{
    public class OutboxPublisherTests
    {
        private LogisticsDbContext CreateContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<LogisticsDbContext>()
                .UseSqlite(connection)
                .Options;
            var ctx = new LogisticsDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private class FlakySender : IMessageSender
        {
            private int _failuresBeforeSuccess;
            private int _calls;

            public FlakySender(int failuresBeforeSuccess)
            {
                _failuresBeforeSuccess = failuresBeforeSuccess;
                _calls = 0;
            }

            public int Calls => _calls;

            public Task SendAsync(Guid messageId, string messageType, string payload)
            {
                _calls++;
                if (_calls <= _failuresBeforeSuccess)
                {
                    throw new InvalidOperationException("transient failure");
                }
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task PublishPending_retries_and_marks_processed_on_success()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using var ctx = CreateContext(conn);

            var msg = new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                MessageType = "Test",
                Payload = "{ \"x\": 1 }",
                OccurredAt = DateTime.UtcNow,
                Processed = false,
                AttemptCount = 0
            };
            ctx.OutboxMessages.Add(msg);
            await ctx.SaveChangesAsync();

            var flaky = new FlakySender(failuresBeforeSuccess: 2);
            var publisher = new OutboxPublisher(ctx, flaky);

            // First publish attempt: will fail and increment attempt count
            var processed = await publisher.PublishPendingAsync();
            processed.Should().Be(0);
            var reloaded = await ctx.OutboxMessages.FindAsync(msg.Id);
            reloaded.AttemptCount.Should().Be(1);
            reloaded.Processed.Should().BeFalse();

            // Second attempt: fail again
            processed = await publisher.PublishPendingAsync();
            processed.Should().Be(0);
            reloaded = await ctx.OutboxMessages.FindAsync(msg.Id);
            reloaded.AttemptCount.Should().Be(2);
            reloaded.Processed.Should().BeFalse();

            // Third attempt: should succeed and mark processed
            processed = await publisher.PublishPendingAsync();
            processed.Should().Be(1);
            reloaded = await ctx.OutboxMessages.FindAsync(msg.Id);
            reloaded.AttemptCount.Should().Be(3);
            reloaded.Processed.Should().BeTrue();

            flaky.Calls.Should().Be(3);
        }
    }
}
