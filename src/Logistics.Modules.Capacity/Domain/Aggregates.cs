using System;

namespace Logistics.Modules.Capacity.Domain
{
    /// <summary>
    /// Capacity aggregate for a voyage
    /// </summary>
    public class VoyageCapacity
    {
        public Guid VoyageId { get; private set; }
        public int TotalCapacity { get; private set; }
        public int HeldCapacity { get; private set; }
        public int ConfirmedCapacity { get; private set; }
        public string OperationalStatus { get; private set; } = "Open";
        public int Version { get; private set; }

        public VoyageCapacity(Guid voyageId, int totalCapacity)
        {
            VoyageId = voyageId;
            TotalCapacity = totalCapacity;
            HeldCapacity = 0;
            ConfirmedCapacity = 0;
            Version = 0;
        }

        public int AvailableCapacity => TotalCapacity - HeldCapacity - ConfirmedCapacity;

        public bool TryReserve(int units)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));
            if (OperationalStatus != "Open") return false;
            if (units > AvailableCapacity) return false;

            HeldCapacity += units;
            Version++;
            return true;
        }

        public void ConfirmReserved(int units)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));
            if (units > HeldCapacity) throw new InvalidOperationException("Not enough held capacity to confirm");

            HeldCapacity -= units;
            ConfirmedCapacity += units;
            Version++;
        }

        public void ReleaseReserved(int units)
        {
            if (units <= 0) throw new ArgumentException("units must be > 0", nameof(units));
            if (units > HeldCapacity) throw new InvalidOperationException("Not enough held capacity to release");

            HeldCapacity -= units;
            Version++;
        }

        public void CloseVoyage()
        {
            OperationalStatus = "Closed";
            Version++;
        }
        internal void RestoreState(int heldCapacity, int confirmedCapacity, string operationalStatus, int version)
        {
            HeldCapacity = heldCapacity;
            ConfirmedCapacity = confirmedCapacity;
            OperationalStatus = operationalStatus;
            Version = version;
        }
    }

    /// <summary>
    /// Capacity Hold aggregate - represents a hold on capacity for a booking
    /// </summary>
    public class CapacityHold
    {
        public Guid HoldId { get; private set; }
        public Guid BookingId { get; private set; }
        public Guid VoyageId { get; private set; }
        public int CapacityUnits { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public string Status { get; private set; } = "Active";
        public int Version { get; private set; }

        public CapacityHold(
            Guid holdId,
            Guid bookingId,
            Guid voyageId,
            int units,
            DateTime createdAt,
            DateTime expiresAt)
        {
            if (units <= 0)
                throw new ArgumentException("units must be > 0", nameof(units));

            if (expiresAt <= createdAt)
                throw new ArgumentException(
                    "expiresAt must be after createdAt",
                    nameof(expiresAt));

            HoldId = holdId;
            BookingId = bookingId;
            VoyageId = voyageId;
            CapacityUnits = units;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
            Version = 0;
        }

        public bool IsExpired(DateTime now) =>
            now >= ExpiresAt && Status == "Active";

        public void Expire()
        {
            if (Status != "Active")
                throw new InvalidOperationException("Hold is not active");

            Status = "Expired";
            Version++;
        }

        public void Confirm()
        {
            if (Status != "Active")
                throw new InvalidOperationException("Hold is not active");

            Status = "Confirmed";
            Version++;
        }

        internal void RestoreState(string status, int version)
        {
            Status = status;
            Version = version;
        }
    }
}
