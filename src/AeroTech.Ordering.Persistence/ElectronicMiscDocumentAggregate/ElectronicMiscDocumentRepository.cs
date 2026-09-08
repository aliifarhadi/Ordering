using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.ElectronicMiscDocumentAggregate
{
    public sealed class ElectronicMiscDocumentRepository : IElectronicMiscDocumentRepository
    {
        private readonly OrderingDbContext _dbContext;

        public ElectronicMiscDocumentRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public Task<ElectronicMiscDocument?> GetAsync(long id, CancellationToken cancellationToken = default)
            => Query().SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

        public async Task<IReadOnlyList<ElectronicMiscDocument>> ListByOrderAsync(long orderId, CancellationToken cancellationToken = default)
            => await Query().Where(document => document.CurrentServicingOrderId == orderId).ToListAsync(cancellationToken);

        public async Task AddAsync(ElectronicMiscDocument document, CancellationToken cancellationToken = default)
            => await _dbContext.Set<ElectronicMiscDocument>().AddAsync(document, cancellationToken);

        private IQueryable<ElectronicMiscDocument> Query()
            => _dbContext.Set<ElectronicMiscDocument>()
                .Include(document => document.Coupons)
                .Include(document => document.PriceLinks);
    }
}
