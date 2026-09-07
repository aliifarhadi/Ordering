using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record BackofficeCreateOrderFromOfferRequest(
        long CustomerId,
        long AirlineOfficeId,
        string OfferId,
        decimal CommissionRate,
        IReadOnlyList<CreateOrderTravellerInput> Travellers,
        CreateOrderContactInput Contact,
        IReadOnlyList<CreateOrderSeatInput> SeatSelections);
}
