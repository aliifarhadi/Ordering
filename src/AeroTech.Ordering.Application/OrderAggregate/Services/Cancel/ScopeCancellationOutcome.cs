using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public sealed record ScopeCancellationOutcome(
        long OrderId,
        long OperationId,
        OrderChangeType Intent,
        long OrderChangeId,
        long? PriceChangeSetId,
        IReadOnlyList<long> CancelledServiceIds,
        IReadOnlyList<long> CancelledItemIds,
        int CommercialVersion,
        long FinancialSequence,
        long ObligationVersion,
        decimal CustomerTotal,
        ProviderOperationOutcome ReservationReleaseOutcome,
        ServicingOperationStatus OperationStatus,
        bool IsReplay);
}
