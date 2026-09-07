using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Framework.Core.ServiceContracts
{
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    }
}
