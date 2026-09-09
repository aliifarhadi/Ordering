using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CancelOrder
{
    public sealed record CancelOrderResult(
        long OrderId,
        long OperationId,
        OrderStatus Status,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        IReadOnlyList<long> CancelledServiceIds,
        ProviderOperationOutcome ReservationReleaseOutcome,
        bool IsReplay);
}
