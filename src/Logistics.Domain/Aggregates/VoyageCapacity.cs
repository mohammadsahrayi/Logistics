using System;

namespace Logistics.Domain.Aggregates
{
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

        public void Close()
        {
            OperationalStatus = "Closed";
            Version++;
        }
    }
}
