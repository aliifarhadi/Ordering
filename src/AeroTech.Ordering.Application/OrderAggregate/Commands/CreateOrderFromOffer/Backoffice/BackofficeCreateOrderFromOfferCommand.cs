using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Backoffice
{
    public sealed record BackofficeCreateOrderFromOfferCommand(
        long CustomerId,
        long CreatorUserId,
        long AirlineOfficeId,
        string OfferId,
        decimal CommissionRate,
        IReadOnlyList<CreateOrderTravellerInput> Travellers,
        CreateOrderContactInput Contact,
        IReadOnlyList<CreateOrderSeatInput> SeatSelections) : IRequest<CreateOrderFromOfferResult>;
}
