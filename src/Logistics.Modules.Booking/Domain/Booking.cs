using System;

namespace Logistics.Modules.Booking.Domain
{
    /// <summary>
    /// Booking aggregate - represents a booking for capacity on a voyage
    /// </summary>
    public enum BookingState { Pending, Confirmed }

    public class Booking
    {
        public Guid BookingId { get; private set; }
        public Guid VoyageId { get; private set; }
        public int RequestedCapacity { get; private set; }
        public BookingState State { get; private set; }
        public Guid? ActiveHoldId { get; private set; }
        public int Version { get; private set; }

        public Booking(Guid bookingId, Guid voyageId, int requestedCapacity)
        {
            if (requestedCapacity <= 0) throw new ArgumentException("requestedCapacity must be > 0", nameof(requestedCapacity));
            BookingId = bookingId;
            VoyageId = voyageId;
            RequestedCapacity = requestedCapacity;
            State = BookingState.Pending;
            ActiveHoldId = null;
            Version = 0;
        }

        public void AttachHold(Guid holdId)
        {
            if (State != BookingState.Pending) throw new InvalidOperationException("Booking not pending");
            if (ActiveHoldId.HasValue && ActiveHoldId.Value != holdId) throw new InvalidOperationException("Booking already has an active hold");
            ActiveHoldId = holdId;
            Version++;
        }

        public void Confirm(Guid holdId)
        {
            if (State == BookingState.Confirmed) return; // idempotent
            if (!ActiveHoldId.HasValue || ActiveHoldId.Value != holdId) throw new InvalidOperationException("No matching active hold");

            State = BookingState.Confirmed;
            Version++;
        }
    }
}
