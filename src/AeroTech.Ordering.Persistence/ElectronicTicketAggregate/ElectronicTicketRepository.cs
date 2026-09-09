using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.ElectronicTicketAggregate
{
    public sealed class ElectronicTicketRepository : IElectronicTicketRepository
    {
        private readonly OrderingDbContext _dbContext;

        public ElectronicTicketRepository(OrderingDbContext dbContext) => _dbContext = dbContext;

        public Task<ElectronicTicket?> GetAsync(long id, CancellationToken cancellationToken = default)
            => Query().SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

        public async Task<IReadOnlyList<ElectronicTicket>> ListByOrderAsync(long orderId, CancellationToken cancellationToken = default)
            => await Query().Where(ticket => ticket.CurrentServicingOrderId == orderId).ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<ElectronicTicket>> ListByOperationAsync(long operationId, CancellationToken cancellationToken = default)
            => await Query().Where(ticket => ticket.OperationId == operationId).ToListAsync(cancellationToken);

        public async Task AddAsync(ElectronicTicket ticket, CancellationToken cancellationToken = default)
            => await _dbContext.Set<ElectronicTicket>().AddAsync(ticket, cancellationToken);

        private IQueryable<ElectronicTicket> Query()
            => _dbContext.Set<ElectronicTicket>()
                .Include(ticket => ticket.Coupons)
                .Include(ticket => ticket.PriceLinks)
                .Include(ticket => ticket.Refunds)
                .ThenInclude(record => record.Coupons);
    }
}
