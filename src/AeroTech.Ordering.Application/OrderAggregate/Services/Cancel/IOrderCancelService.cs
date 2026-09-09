using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public interface IOrderCancelService
    {
        Task<CancelOrderOutcome> CancelAsync(
            long orderId,
            VoidReason reason,
            long cancelledBy,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }

    public sealed record CancelOrderOutcome(
        long OrderId,
        long OperationId,
        OrderStatus Status,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        long FinancialSequence,
        IReadOnlyList<long> CancelledServiceIds,
        ProviderOperationOutcome ReservationReleaseOutcome,
        bool IsReplay);
}
