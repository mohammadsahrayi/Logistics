using System;
using System.Threading.Tasks;

namespace Logistics.Application.Contracts
{
    public record CreateHoldResult(bool Success, Guid? HoldId, string? Reason);

    public interface ICapacityService
    {
        Task<CreateHoldResult> CreateHoldAsync(Guid bookingId, Guid voyageId, int units, TimeSpan ttl, string? idempotencyKey = null);
        Task<(bool Success, string? Reason)> ConfirmBookingAsync(Guid bookingId, Guid holdId, string? idempotencyKey = null);
    }
}
