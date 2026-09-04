namespace Logistics.Shared.Messaging
{
    /// <summary>
    /// Publishes outbox messages to external systems (or could stay internal for event consumption)
    /// </summary>
    public interface IMessageSender
    {
        Task SendAsync(IntegrationEvent @event, CancellationToken ct = default);
    }

    public record IntegrationEvent(string Type, string Payload);
}
