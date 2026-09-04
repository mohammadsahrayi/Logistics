using Logistics.Infrastructure.Persistence;
using Logistics.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Logistics.Infrastructure.Services
{
    public class OutboxPublisher
    {
        private readonly LogisticsDbContext _db;
        private readonly IMessageSender _sender;
        private readonly IIntegrationEventDeserializer _deserializer;
        private readonly ILogger<OutboxPublisher> _logger;

        public OutboxPublisher(
            LogisticsDbContext db,
            IMessageSender sender,
            IIntegrationEventDeserializer deserializer,
            ILogger<OutboxPublisher> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _deserializer = deserializer
                ?? throw new ArgumentNullException(nameof(deserializer));
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> PublishPendingAsync(
            int batchSize = 10,
            CancellationToken ct = default)
        {
            var pending = await _db.OutboxMessages
                .Where(m => !m.Processed)
                .OrderBy(m => m.OccurredAt)
                .Take(batchSize)
                .ToListAsync(ct);

            var processedCount = 0;

            LogisticsMetrics.OutboxBacklog.Record(pending.Count);

            foreach (var msg in pending)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var integrationEvent = _deserializer.Deserialize(
                        msg.MessageType,
                        msg.Payload);

                    await _sender.SendAsync(
                        integrationEvent,
                        ct);

                    msg.Processed = true;
                    msg.PublishedAt = DateTime.UtcNow;
                    msg.AttemptCount++;
                    msg.LastError = null;

                    await _db.SaveChangesAsync(ct);

                    _logger.LogInformation(
                        "Outbox message published {MessageId} of type {MessageType}",
                        msg.Id,
                        msg.MessageType);

                    processedCount++;
                }
                catch (Exception ex)
                {
                    msg.AttemptCount++;
                    msg.LastError = ex.Message;

                    try
                    {
                        await _db.SaveChangesAsync(ct);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(
                            saveEx,
                            "Failed to persist failure state for outbox message {MessageId}",
                            msg.Id);
                    }

                    _logger.LogWarning(
                        ex,
                        "Outbox publication failed {MessageId} of type {MessageType}",
                        msg.Id,
                        msg.MessageType);
                }
            }

            return processedCount;
        }
    }

}
