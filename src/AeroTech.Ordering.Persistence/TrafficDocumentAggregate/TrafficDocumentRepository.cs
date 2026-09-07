using AeroTech.Ordering.Domain.TrafficDocumentAggregate;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.TrafficDocumentAggregate
{
    public sealed class TrafficDocumentRepository : ITrafficDocumentRepository
    {
        private readonly OrderingDbContext _dbContext;

        public TrafficDocumentRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(TrafficDocument document, CancellationToken cancellationToken = default)
            => await _dbContext.Set<TrafficDocument>().AddAsync(document, cancellationToken);

        public Task<TrafficDocument?> GetAsync(long id, CancellationToken cancellationToken = default)
            => _dbContext.Set<TrafficDocument>()
                .Include(document => document.Coupons)
                .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

        public async Task<IReadOnlyList<TrafficDocument>> GetByOrderAsync(long orderId, CancellationToken cancellationToken = default)
            => await _dbContext.Set<TrafficDocument>()
                .Include(document => document.Coupons)
                .Where(document => document.OrderId == orderId)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<TrafficDocument>> GetByOrderAndTravellersAsync(long orderId, IReadOnlyCollection<long> travellerIds, CancellationToken cancellationToken = default)
            => await _dbContext.Set<TrafficDocument>()
                .Include(document => document.Coupons)
                .Where(document => document.OrderId == orderId && travellerIds.Contains(document.TravellerId))
                .ToListAsync(cancellationToken);

        public Task<bool> DocumentNumberExistsAsync(string documentNumber, CancellationToken cancellationToken = default)
            => _dbContext.Set<TrafficDocument>().AnyAsync(document => document.DocumentNumber == documentNumber, cancellationToken);
    }
}
