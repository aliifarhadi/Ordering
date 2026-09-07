using AeroTech.Messages.Ordering.Enums;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.PayOrder
{
    public sealed record PayOrderCommand(
        long OrderId,
        FormOfPayment FormOfPayment,
        string? WalletReference) : IRequest<PayOrderResult>;
}
