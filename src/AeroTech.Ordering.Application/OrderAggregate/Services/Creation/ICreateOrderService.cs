using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Creation
{
    public interface ICreateOrderService
    {
        Task<CreateOrderOutcome> CreateAsync(
            CreateOrderArgs args,
            AcceptedOrderSource source,
            string idempotencyKey,
            CancellationToken cancellationToken = default);
    }
}
