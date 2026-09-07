using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Framework.Core.ServiceContracts
{
    public interface IOutboxWriter
    {
        Task WriteAsync(object message, IDomainEvent source, CancellationToken cancellationToken = default);
    }
}
