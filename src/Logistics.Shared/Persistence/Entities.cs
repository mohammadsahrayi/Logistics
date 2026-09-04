using System;

namespace Logistics.Shared.Persistence
{
    // Capacity Module Entities
    public class VoyageCapacityEntity
    {
        public Guid VoyageId { get; set; }
        public int TotalCapacity { get; set; }
        public int HeldCapacity { get; set; }
        public int ConfirmedCapacity { get; set; }
        public string OperationalStatus { get; set; } = "Open";
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CapacityHoldEntity
    {
        public Guid HoldId { get; set; }
        public Guid BookingId { get; set; }
        public Guid VoyageId { get; set; }
        public int CapacityUnits { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; } = "Active";
        public int Version { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Booking Module Entities
    public class BookingEntity
    {
        public Guid BookingId { get; set; }
        public Guid VoyageId { get; set; }
        public int RequestedCapacity { get; set; }
        public string State { get; set; } = "Pending";
        public Guid? ActiveHoldId { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Outbox/Inbox Entities
    public class OutboxMessageEntity
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public bool Processed { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public class InboxEntryEntity
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public bool Processed { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public class BookingConfirmationProjectionEntity
    {
        public Guid BookingId { get; set; }
        public string State { get; set; } = string.Empty;
        public DateTime ConfirmedAt { get; set; }
    }

    public class IdempotencyEntryEntity
    {
        public string IdempotencyKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string RequestHash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ResponseStatusCode { get; set; }
        public string? ResponseBody { get; set; }
        public string? ResultJson { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
