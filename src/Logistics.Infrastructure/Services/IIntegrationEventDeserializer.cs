using Logistics.Application.Contracts;
using Logistics.Shared.Messaging;
using System.Text.Json;

namespace Logistics.Infrastructure.Services
{
    public interface IIntegrationEventDeserializer
    {
        IntegrationEvent Deserialize(string messageType, string payload);
    }

    public sealed class IntegrationEventDeserializer : IIntegrationEventDeserializer
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public IntegrationEventDeserializer()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public IntegrationEvent Deserialize(
            string messageType,
            string payload)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException(
                    "Message type is required.",
                    nameof(messageType));

            if (string.IsNullOrWhiteSpace(payload))
                throw new ArgumentException(
                    "Payload is required.",
                    nameof(payload));

            // Validate that payload is valid JSON.
            using var document = JsonDocument.Parse(payload);

            return new IntegrationEvent(
                messageType,
                payload);
        }
    }
}
