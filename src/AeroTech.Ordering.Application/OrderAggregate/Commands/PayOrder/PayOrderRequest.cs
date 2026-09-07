using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.PayOrder
{
    public sealed record PayOrderRequest(FormOfPayment FormOfPayment, string? WalletReference);
}
