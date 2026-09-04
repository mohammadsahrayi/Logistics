using System;
using System.Threading.Tasks;

namespace Logistics.Shared
{
    public interface IClock
    {
        Task<DateTime> GetUtcNowAsync();
    }
}
