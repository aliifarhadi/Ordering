using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrderItem
{
    public sealed record CancelOrderItemCommand(
        long OrderId,
        long OrderItemId,
        string QuotedCancellationId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<ScopeCancellationOutcome>;
}
