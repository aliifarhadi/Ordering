using AeroTech.Messages.Ordering.Enums;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrder
{
    public sealed record CancelOrderCommand(
        long OrderId,
        string IdempotencyKey,
        VoidReason Reason = VoidReason.CustomerRequest,
        int? ExpectedCommercialVersion = null) : IRequest<CancelOrderResult>;
}
