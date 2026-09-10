using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.RefundDocument
{
    public sealed record RefundDocumentCommand(
        long OrderId,
        long ElectronicTicketId,
        IReadOnlyList<long> TicketCouponIds,
        string IdempotencyKey,
        int? ExpectedCommercialVersion,
        string? QuotedRefundId = null,
        ManualRefundInstruction? Manual = null) : IRequest<RefundOutcome>;
}
