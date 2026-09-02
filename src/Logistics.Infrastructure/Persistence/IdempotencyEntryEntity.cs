using System;

namespace Logistics.Infrastructure.Persistence
{
    public class IdempotencyEntryEntity
    {
        public string IdempotencyKey { get; set; }
        public DateTime CreatedAt { get; set; }

        // Request fingerprint to detect same-key/different-payload
        public string? RequestHash { get; set; }

        // Status: Pending, Completed, Failed
        public string? Status { get; set; }

        // Response metadata
        public int? ResponseStatusCode { get; set; }
        public string? ResponseBody { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string? ResultJson { get; set; }
    }
}
