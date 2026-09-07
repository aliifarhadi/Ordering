using System.ComponentModel.DataAnnotations;
using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.Domain.Events;

namespace AeroTech.Framework.Core.Domain.Aggregates
{
    public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : notnull
    {
        private readonly List<IDomainEvent> _events = new();

        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        protected AggregateRoot()
        {
        }

        protected void Causes(IDomainEvent domainEvent) => _events.Add(domainEvent);

        public IEnumerable<IDomainEvent> GetEvents() => _events.AsEnumerable();

        public void ClearEvents() => _events.Clear();
    }
}
