using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer
{
    public sealed record CreateOrderFromOfferCommand(
        long CustomerId,
        SalesChannel Channel,
        long CreatorUserId,
        long AirlineOfficeId,
        string OfferId,
        decimal CommissionRate,
        IReadOnlyList<CreateOrderTravellerInput> Travellers,
        CreateOrderContactInput Contact,
        IReadOnlyList<CreateOrderSeatInput> SeatSelections) : IRequest<CreateOrderFromOfferResult>;
}
