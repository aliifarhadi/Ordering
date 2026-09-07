using AeroTech.Framework.Core.Domain.Aggregates;

namespace AeroTech.Framework.Core.Domain.Repository
{
    public interface IRepository<TAggregate, in TId>
        where TAggregate : class, IAggregateRoot
        where TId : notnull
    {
        Task<TAggregate?> GetAsync(TId id, CancellationToken cancellationToken = default);

        Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

        void Update(TAggregate aggregate);

        void Remove(TAggregate aggregate);
    }
}
