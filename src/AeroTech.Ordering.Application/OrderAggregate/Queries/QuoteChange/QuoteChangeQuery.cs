using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Queries.QuoteChange
{
    public sealed record QuoteChangeQuery(
        long OrderId,
        long OrderServiceId) : IRequest<ChangeQuoteOutcome>;
}
