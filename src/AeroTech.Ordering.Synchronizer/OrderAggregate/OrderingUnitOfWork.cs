using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Ordering.Persistence;
using AeroTech.Ordering.Query._Shared.DbContexts;

namespace AeroTech.Ordering.Synchronizer.OrderAggregate
{
    public sealed class OrderingUnitOfWork : IUnitOfWork
    {
        private readonly OrderingDbContext _commandDbContext;
        private readonly OrderQueryDbContext _queryDbContext;

        public OrderingUnitOfWork(OrderingDbContext commandDbContext, OrderQueryDbContext queryDbContext)
        {
            _commandDbContext = commandDbContext;
            _queryDbContext = queryDbContext;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var affected = await _commandDbContext.SaveChangesAsync(cancellationToken);
            await _queryDbContext.SaveChangesAsync(cancellationToken);
            return affected;
        }
    }
}
