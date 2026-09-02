using System;
using System.Threading.Tasks;

namespace Logistics.Infrastructure.Services
{
    public interface IMessageSender
    {
        /// <summary>
        /// Sends a message to the external system. Implementations should be idempotent with respect to message id.
        /// </summary>
        Task SendAsync(Guid messageId, string messageType, string payload);
    }
}
