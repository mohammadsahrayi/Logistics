using System;

namespace Logistics.Modules.Capacity.Domain
{
    /// <summary>
    /// Repository interface for VoyageCapacity aggregate
    /// </summary>
    public interface IVoyageCapacityRepository
    {
        Task<bool> TryReserveAtomic(Guid voyageId, int units);
        Task ConfirmReserved(Guid voyageId, int units);
        Task ReleaseReserved(Guid voyageId, int units);
        Task<VoyageCapacity?> GetByIdAsync(Guid voyageId);
        Task SaveAsync(VoyageCapacity voyage);
    }

    /// <summary>
    /// Repository interface for CapacityHold aggregate
    /// </summary>
    public interface ICapacityHoldRepository
    {
        Task<CapacityHold?> GetByIdAsync(Guid holdId);
        Task SaveAsync(CapacityHold hold);
        Task<IEnumerable<CapacityHold>> GetExpiredHoldsAsync(DateTime now);
    }
}
