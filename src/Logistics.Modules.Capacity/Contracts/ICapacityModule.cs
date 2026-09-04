using System;

namespace Logistics.Modules.Capacity.Contracts
{
    /// <summary>
    /// Public interface for the Capacity Module
    /// Only other modules should reference this interface, not internal domain/application classes
    /// </summary>
    public interface ICapacityModule
    {
        Task<CreateHoldResult> CreateHoldAsync(Guid bookingId, Guid voyageId, int units, TimeSpan ttl, string? idempotencyKey = null);
        Task<(bool Success, string? Reason)> ConfirmBookingAsync(Guid bookingId, Guid holdId, string? idempotencyKey = null);
        Task<CapacityHoldResult?> GetCapacityHoldAsync(Guid bookingId);
        Task<VoyageCapacityResult?> GetVoyageCapacityAsync(Guid voyageId);
    }

    // DTOs - these are the only data contracts exposed by the module
    public record CreateHoldResult(bool Success, Guid? HoldId, string? Reason);
    public record CapacityHoldResult(Guid HoldId, Guid BookingId, Guid VoyageId, int CapacityUnits, DateTime CreatedAt, DateTime ExpiresAt, string Status);
    public record VoyageCapacityResult(Guid VoyageId, int TotalCapacity, int HeldCapacity, int ConfirmedCapacity, int AvailableCapacity, string OperationalStatus);
}
