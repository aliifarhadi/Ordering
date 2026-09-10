using AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelRefund
{
    public sealed record CancelRefundCommand(
        long OrderId,
        long ElectronicTicketId,
        long RefundRecordId,
        string Reason,
        string IdempotencyKey,
        int? ExpectedCommercialVersion,
        string? ReasonDetail = null) : IRequest<CancelRefundOutcome>;
}
