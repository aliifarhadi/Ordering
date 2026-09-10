using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.WithdrawOrder
{
    public sealed record WithdrawOrderCommand(
        long OrderId,
        VoidReason Reason,
        string IdempotencyKey,
        int? ExpectedCommercialVersion) : IRequest<WithdrawOrderOutcome>;
}
