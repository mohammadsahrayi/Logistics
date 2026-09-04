using Logistics.Modules.Capacity.Application;
using Logistics.Modules.Capacity.Contracts;
using Logistics.Modules.Capacity.Infrastructure;
using Logistics.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Logistics.Modules.Capacity
{
    /// <summary>
    /// Extension methods to register the Capacity Module in the DI container
    /// </summary>
    public static class CapacityModuleExtensions
    {
        /// <summary>
        /// Registers all Capacity module services
        /// </summary>
        public static IServiceCollection AddCapacityModule(this IServiceCollection services, DbContext dbContext)
        {
            // Register repositories
            services.AddScoped<VoyageCapacityRepository>();
            services.AddScoped<CapacityHoldRepository>();

            // Register the public module interface
            services.AddScoped<ICapacityModule>(sp =>
            {
                var db = sp.GetRequiredService<DbContext>();
                var voyageRepo = sp.GetRequiredService<VoyageCapacityRepository>();
                var clock = sp.GetRequiredService<IClock>();
                var logger = sp.GetRequiredService<ILogger<CapacityApplicationService>>();
                return new CapacityApplicationService(db, voyageRepo, clock, logger);
            });

            return services;
        }
    }
}
