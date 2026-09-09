using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public sealed record CancelOrderOutcome(
        long OrderId,
        long OperationId,
        OrderStatus Status,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        long FinancialSequence,
        IReadOnlyList<long> CancelledServiceIds,
        ProviderOperationOutcome ReservationReleaseOutcome,
        ServicingOperationStatus OperationStatus,
        bool IsReplay);
}
