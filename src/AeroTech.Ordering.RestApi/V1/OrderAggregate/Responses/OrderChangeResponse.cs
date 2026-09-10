using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Query.OrderAggregate.View;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Responses
{
    public sealed record OrderChangeResponse(
        long OperationId,
        int CommercialVersion,
        OrderView? Order,
        ExchangeOutcome? Exchange = null);
}
