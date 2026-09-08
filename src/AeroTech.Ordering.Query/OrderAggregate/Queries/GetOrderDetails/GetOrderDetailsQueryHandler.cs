using System.Text.Json;
using AeroTech.Ordering.Query.OrderAggregate.View;
using AeroTech.Ordering.Query._Shared.DbContexts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails
{
    public sealed class GetOrderDetailsQueryHandler : IRequestHandler<GetOrderDetailsQuery, OrderView?>
    {
        private static readonly JsonSerializerOptions SnapshotOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly OrderQueryDbContext _dbContext;

        public GetOrderDetailsQueryHandler(OrderQueryDbContext dbContext) => _dbContext = dbContext;

        public async Task<OrderView?> Handle(GetOrderDetailsQuery request, CancellationToken cancellationToken)
        {
            var row = await _dbContext.OrderDetails
                .AsNoTracking()
                .SingleOrDefaultAsync(details => details.Id == request.OrderId, cancellationToken);

            return row is null ? null : JsonSerializer.Deserialize<OrderView>(row.SnapshotJson, SnapshotOptions);
        }
    }
}
