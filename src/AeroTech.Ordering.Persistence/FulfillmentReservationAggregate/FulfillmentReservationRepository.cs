using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.FulfillmentReservationAggregate
{
    public sealed class FulfillmentReservationRepository : IFulfillmentReservationRepository
    {
        private readonly OrderingDbContext _dbContext;

        public FulfillmentReservationRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public Task<FulfillmentReservation?> GetAsync(long id, CancellationToken cancellationToken = default)
            => Query().SingleOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

        public Task<FulfillmentReservation?> GetByOperationAsync(long operationId, CancellationToken cancellationToken = default)
            => Query().SingleOrDefaultAsync(reservation => reservation.OperationId == operationId, cancellationToken);

        public async Task<IReadOnlyList<FulfillmentReservation>> ListByOrderAsync(long orderId, CancellationToken cancellationToken = default)
            => await Query().Where(reservation => reservation.OrderId == orderId).ToListAsync(cancellationToken);

        public async Task AddAsync(FulfillmentReservation reservation, CancellationToken cancellationToken = default)
            => await _dbContext.Set<FulfillmentReservation>().AddAsync(reservation, cancellationToken);

        private IQueryable<FulfillmentReservation> Query()
            => _dbContext.Set<FulfillmentReservation>().Include(reservation => reservation.Services);
    }
}
