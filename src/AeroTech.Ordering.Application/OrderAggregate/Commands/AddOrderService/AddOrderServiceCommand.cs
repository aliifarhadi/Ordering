using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderService
{
    public sealed record AddOrderServiceCommand(
        long OrderId,
        IReadOnlyList<SelectedQuotedOffer> AcceptSelectedQuotedOfferList,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<OrderChangeOutcome>;
}
