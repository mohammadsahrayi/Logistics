using System;

namespace Logistics.Domain.Aggregates
{
    public enum HoldStatus { Active, Confirmed, Expired }

    public class CapacityHold
    {
        public Guid HoldId { get; private set; }
        public Guid BookingId { get; private set; }
        public Guid VoyageId { get; private set; }
        public int CapacityUnits { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public HoldStatus Status { get; private set; }
        public int Version { get; private set; }

        public CapacityHold(Guid holdId, Guid bookingId, Guid voyageId, int units, DateTime createdAt, TimeSpan ttl)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));
            HoldId = holdId;
            BookingId = bookingId;
            VoyageId = voyageId;
            CapacityUnits = units;
            CreatedAt = createdAt;
            ExpiresAt = createdAt.Add(ttl);
            Status = HoldStatus.Active;
            Version = 0;
        }

        public bool IsExpired(DateTime now) => Status == HoldStatus.Active && now >= ExpiresAt;

        public void Confirm(DateTime now)
        {
            if (Status != HoldStatus.Active) throw new InvalidOperationException("Hold not active");
            if (now >= ExpiresAt) throw new InvalidOperationException("Hold already expired");

            Status = HoldStatus.Confirmed;
            Version++;
        }

        public void Expire(DateTime now)
        {
            if (Status != HoldStatus.Active) return; // idempotent
            if (now < ExpiresAt) throw new InvalidOperationException("Cannot expire before expiry time");

            Status = HoldStatus.Expired;
            Version++;
        }
    }
}
