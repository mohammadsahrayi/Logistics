using Logistics.Modules.Booking.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Logistics.Modules.Booking
{
    /// <summary>
    /// Extension methods to register the Booking Module in the DI container
    /// </summary>
    public static class BookingModuleExtensions
    {
        /// <summary>
        /// Registers all Booking module services
        /// </summary>
        public static IServiceCollection AddBookingModule(this IServiceCollection services)
        {
            // Register booking-specific services here
            // For now, booking functionality is integrated with the Capacity module

            return services;
        }
    }
}
