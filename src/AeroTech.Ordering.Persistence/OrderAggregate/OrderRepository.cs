using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderRepository : IOrderRepository
    {
        private static readonly OrderStatus[] ExpirableStatuses =
        {
            OrderStatus.Created,
            OrderStatus.Confirmed,
            OrderStatus.ReservationUnconfirmed
        };

        private readonly OrderingDbContext _dbContext;

        public OrderRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => await _dbContext.Set<Order>().AddAsync(order, cancellationToken);

        public Task<Order?> GetAsync(long id, CancellationToken cancellationToken = default)
            => AggregateQuery().FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

        private IQueryable<Order> AggregateQuery()
            => _dbContext.Set<Order>()
                .Include(order => order.Items).ThenInclude(item => item.PolicySnapshot)
                .Include(order => order.Items).ThenInclude(item => item.ProductSnapshot)
                .Include(order => order.Items).ThenInclude(item => item.CommercialTermsSnapshot)
                .Include(order => order.PricingLines).ThenInclude(line => line.AllocationSets).ThenInclude(set => set.Allocations)
                .Include(order => order.PriceChangeSets)
                .Include(order => order.Changes)
                .Include(order => order.Travellers).ThenInclude(traveller => traveller.Documents)
                .Include(order => order.Segments).ThenInclude(segment => segment.Legs)
                .Include(order => order.OrderServices)
                .Include(order => order.Itineraries)
                .Include(order => order.TimeLimits)
                .Include(order => order.ExternalReferences)
                .Include(order => order.Contact!).ThenInclude(contact => contact.ContactPoints)
                .AsSplitQuery();

        public Task<bool> RecordLocatorExistsAsync(string recordLocator, CancellationToken cancellationToken = default)
            => _dbContext.Set<Order>()
                .AnyAsync(order => order.RecordLocator!.Value == recordLocator, cancellationToken);

        public async Task<IReadOnlyList<long>> GetExpiredOrderIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default)
            => await _dbContext.Set<Order>()
                .Where(order => order.TimeToLive != null && order.TimeToLive <= now && ExpirableStatuses.Contains(order.Status))
                .OrderBy(order => order.TimeToLive)
                .Take(batchSize)
                .Select(order => order.Id)
                .ToListAsync(cancellationToken);
    }
}
