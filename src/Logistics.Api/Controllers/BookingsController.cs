using Logistics.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Logistics.Api.Controllers
{
    [ApiController]
    [Route("api/bookings/{bookingId:guid}")]
    public class BookingsController : ControllerBase
    {
        private readonly ICapacityService _capacityService;

        public BookingsController(ICapacityService capacityService)
        {
            _capacityService = capacityService;
        }

        [HttpPost("/api/bookings/{bookingId:guid}/capacity-holds")]
        public async Task<IActionResult> CreateHold([FromRoute] Guid bookingId, [FromBody] CreateHoldRequest req)
        {
            var idempotencyKey = Request.Headers.ContainsKey("Idempotency-Key") ? Request.Headers["Idempotency-Key"].ToString() : null;
            var result = await _capacityService.CreateHoldAsync(bookingId, req.VoyageId, req.Units, TimeSpan.FromMinutes(req.TtlMinutes), idempotencyKey);
            if (!result.Success) return BadRequest(new { reason = result.Reason });
            return CreatedAtAction(nameof(GetHold), new { bookingId = bookingId }, new { holdId = result.HoldId });
        }

        [HttpPost("/api/bookings/{bookingId:guid}/confirm")]
        public async Task<IActionResult> Confirm([FromRoute] Guid bookingId, [FromBody] ConfirmRequest req)
        {
            var idempotencyKey = Request.Headers.ContainsKey("Idempotency-Key") ? Request.Headers["Idempotency-Key"].ToString() : null;
            var (success, reason) = await _capacityService.ConfirmBookingAsync(bookingId, req.HoldId, idempotencyKey);
            if (!success) return BadRequest(new { reason });
            return Ok();
        }

        [HttpGet("/api/bookings/{bookingId:guid}/capacity-hold")]
        public async Task<IActionResult> GetHold([FromRoute] Guid bookingId)
        {
            var hold = await _capacityService.GetCapacityHoldAsync(bookingId);
            return hold == null ? NotFound() : Ok(hold);
        }
    }

    [ApiController]
    [Route("api/voyages")]
    public class VoyagesController : ControllerBase
    {
        private readonly ICapacityService _capacityService;

        public VoyagesController(ICapacityService capacityService)
        {
            _capacityService = capacityService;
        }

        [HttpGet("{voyageId:guid}/capacity")]
        public async Task<IActionResult> GetCapacity([FromRoute] Guid voyageId)
        {
            var capacity = await _capacityService.GetVoyageCapacityAsync(voyageId);
            return capacity == null ? NotFound() : Ok(capacity);
        }
    }

    public record CreateHoldRequest(Guid VoyageId, int Units, int TtlMinutes);
    public record ConfirmRequest(Guid HoldId);
}
