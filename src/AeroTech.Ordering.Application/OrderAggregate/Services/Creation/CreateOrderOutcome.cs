using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Creation
{
    public sealed record CreateOrderOutcome(
        long OrderId,
        long ReceiptId,
        int CommercialVersion,
        CommercialSummary CommercialSummary,
        bool IsReplay);
}
