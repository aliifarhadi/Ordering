using AeroTech.Ordering.Query.OrderAggregate.Dto;
using AeroTech.Ordering.Query._Shared.DbContexts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderById
{
    public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
    {
        private readonly OrderQueryDbContext _dbContext;

        public GetOrderByIdQueryHandler(OrderQueryDbContext dbContext) => _dbContext = dbContext;

        public async Task<OrderDto?> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
        {
            return await _dbContext.Orders
                .AsNoTracking()
                .Where(order => order.Id == query.OrderId)
                .Select(order => new OrderDto(
                    order.Id,
                    order.RecordLocator,
                    order.Status,
                    order.Channel,
                    order.CustomerId,
                    order.AirlineOfficeId,
                    order.CurrencyId,
                    order.Pax,
                    order.GrandTotal,
                    order.TotalTax,
                    order.CreationDate))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
