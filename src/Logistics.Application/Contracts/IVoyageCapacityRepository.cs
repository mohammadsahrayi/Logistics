using System;
using System.Threading.Tasks;

namespace Logistics.Application.Contracts
{
    public interface IVoyageCapacityRepository
    {
        /// <summary>
        /// Try to reserve capacity atomically. Returns true when reserved, false when insufficient capacity or closed.
        /// This method must be safe under concurrent calls for the same voyage.
        /// </summary>
        Task<bool> TryReserveAtomic(Guid voyageId, int units);

        /// <summary>
        /// Confirm reserved capacity: convert held -> confirmed. Must be called when hold is being confirmed.
        /// </summary>
        Task ConfirmReserved(Guid voyageId, int units);

        /// <summary>
        /// Release reserved capacity: used when a hold expires.
        /// </summary>
        Task ReleaseReserved(Guid voyageId, int units);
    }
}
