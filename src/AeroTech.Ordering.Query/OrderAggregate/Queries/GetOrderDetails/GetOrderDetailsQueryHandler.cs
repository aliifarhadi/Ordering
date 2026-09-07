using System.Text.Json;
using AeroTech.Ordering.Query._Shared.DbContexts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails
{
    public sealed class GetOrderDetailsQueryHandler : IRequestHandler<GetOrderDetailsQuery, OrderDetailsDto?>
    {
        private readonly OrderQueryDbContext _dbContext;

        public GetOrderDetailsQueryHandler(OrderQueryDbContext dbContext) => _dbContext = dbContext;

        public async Task<OrderDetailsDto?> Handle(GetOrderDetailsQuery request, CancellationToken cancellationToken)
        {
            var row = await _dbContext.OrderDetails
                .AsNoTracking()
                .SingleOrDefaultAsync(details => details.Id == request.OrderId, cancellationToken);

            if (row is null)
                return null;

            var snapshot = JsonSerializer.Deserialize<JsonElement>(row.SnapshotJson);

            return new OrderDetailsDto(row.Id, row.ProjectionRevision, row.UpdatedAt, snapshot);
        }
    }
}
