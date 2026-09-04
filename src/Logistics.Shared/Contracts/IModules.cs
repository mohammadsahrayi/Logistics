using System;

namespace Logistics.Shared.Contracts
{
    /// <summary>
    /// Public API for the Capacity Module
    /// </summary>
    public interface ICapacityModule
    {
        Task<CreateHoldResult> CreateHoldAsync(Guid bookingId, Guid voyageId, int units, TimeSpan ttl, string? idempotencyKey = null);
        Task<(bool Success, string? Reason)> ConfirmBookingAsync(Guid bookingId, Guid holdId, string? idempotencyKey = null);
        Task<CapacityHoldResult?> GetCapacityHoldAsync(Guid bookingId);
        Task<VoyageCapacityResult?> GetVoyageCapacityAsync(Guid voyageId);
    }

    /// <summary>
    /// Public API for the Booking Module
    /// </summary>
    public interface IBookingModule
    {
        // To be implemented - booking operations
    }

    // Capacity Module DTOs
    public record CreateHoldResult(bool Success, Guid? HoldId, string? Reason);
    public record CapacityHoldResult(Guid HoldId, Guid BookingId, Guid VoyageId, int CapacityUnits, DateTime CreatedAt, DateTime ExpiresAt, string Status);
    public record VoyageCapacityResult(Guid VoyageId, int TotalCapacity, int HeldCapacity, int ConfirmedCapacity, int AvailableCapacity, string OperationalStatus);

    // Clock abstraction (used by multiple modules)
    public interface IClock
    {
        Task<DateTime> GetUtcNowAsync();
    }
}
