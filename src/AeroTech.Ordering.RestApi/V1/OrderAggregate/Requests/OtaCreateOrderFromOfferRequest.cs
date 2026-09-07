using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record OtaCreateOrderFromOfferRequest(
        string OfferId,
        OtaOrderContact Contact,
        IReadOnlyList<OtaOrderTraveller> Travellers);
}
