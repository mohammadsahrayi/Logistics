using Logistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Logistics.Infrastructure.Services
{
    public class OutboxPublisher
    {
        private readonly LogisticsDbContext _db;
        private readonly IMessageSender _sender;
        private readonly ILogger<OutboxPublisher> _logger;

        public OutboxPublisher(LogisticsDbContext db, IMessageSender sender)
            : this(db, sender, Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboxPublisher>.Instance)
        {
        }

        public OutboxPublisher(LogisticsDbContext db, IMessageSender sender, ILogger<OutboxPublisher> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> PublishPendingAsync(int batchSize = 10, CancellationToken ct = default)
        {
            // Select pending messages
            var pending = await _db.OutboxMessages
                .Where(m => !m.Processed)
                .OrderBy(m => m.OccurredAt)
                .Take(batchSize)
                .ToListAsync(ct);

            var processedCount = 0;
            LogisticsMetrics.OutboxBacklog.Record(pending.Count);

            foreach (var msg in pending)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // Attempt to send
                    await _sender.SendAsync(msg.Id, msg.MessageType, msg.Payload);

                    // Mark as processed
                    msg.Processed = true;
                    msg.PublishedAt = DateTime.UtcNow;
                    msg.AttemptCount += 1;
                    msg.LastError = null;

                    _db.OutboxMessages.Update(msg);
                    await _db.SaveChangesAsync(ct);

                    _logger.LogInformation("Outbox message published {MessageId} of type {MessageType}", msg.Id, msg.MessageType);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    // Record failure and increment attempt count
                    msg.AttemptCount += 1;
                    msg.LastError = ex.Message;
                    _db.OutboxMessages.Update(msg);
                    await _db.SaveChangesAsync(ct);
                    _logger.LogWarning(ex, "Outbox publication failed {MessageId} of type {MessageType}", msg.Id, msg.MessageType);
                    // continue with other messages
                }
            }

            return processedCount;
        }
    }
}
