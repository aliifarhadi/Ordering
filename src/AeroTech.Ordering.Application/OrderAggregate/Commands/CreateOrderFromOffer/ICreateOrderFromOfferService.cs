using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer
{
    public interface ICreateOrderFromOfferService
    {
        Task<CreateOrderFromOfferResult> ExecuteAsync(
            CreateOrderArgs args,
            CancellationToken cancellationToken = default);
    }
}
