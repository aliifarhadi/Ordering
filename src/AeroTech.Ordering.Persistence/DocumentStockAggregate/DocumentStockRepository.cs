using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.DocumentStockAggregate
{
    public sealed class DocumentStockRepository : IDocumentStockRepository
    {
        private readonly OrderingDbContext _dbContext;

        public DocumentStockRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public Task<DocumentStock?> GetActiveForOperationAsync(
            long ownerAirlineId,
            string documentType,
            long operationId,
            CancellationToken cancellationToken = default)
            => _dbContext.Set<DocumentStock>()
                .Include(stock => stock.Allocations.Where(allocation => allocation.OperationId == operationId))
                .Where(stock => stock.OwnerAirlineId == ownerAirlineId
                                && stock.DocumentType == documentType
                                && stock.Status == DocumentStockStatus.Active)
                .OrderBy(stock => stock.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<DocumentStock?> GetAsync(long id, CancellationToken cancellationToken = default)
            => Query().SingleOrDefaultAsync(stock => stock.Id == id, cancellationToken);

        public async Task AddAsync(DocumentStock stock, CancellationToken cancellationToken = default)
            => await _dbContext.Set<DocumentStock>().AddAsync(stock, cancellationToken);

        private IQueryable<DocumentStock> Query()
            => _dbContext.Set<DocumentStock>().Include(stock => stock.Allocations);
    }
}
