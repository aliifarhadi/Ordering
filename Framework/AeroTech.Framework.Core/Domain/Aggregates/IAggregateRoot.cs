using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Framework.Core.Domain.Aggregates
{
    public interface IAggregateRoot : IEntity
    {
        void ClearEvents();

        IEnumerable<IDomainEvent> GetEvents();
    }
}
