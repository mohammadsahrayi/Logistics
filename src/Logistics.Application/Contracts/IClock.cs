using System;
using System.Threading.Tasks;

namespace Logistics.Application.Contracts
{
    public interface IClock
    {
        /// <summary>
        /// Returns authoritative UTC time from the database or server.
        /// Implementations should prefer database time for transactional decisions.
        /// </summary>
        Task<DateTime> GetUtcNowAsync();
    }
}
