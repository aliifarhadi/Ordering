using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed record RefundOutcome(
        long OrderId,
        long OperationId,
        long ElectronicTicketId,
        string DocumentNumber,
        int DocumentVersion,
        long? RefundRecordId,
        long? OrderChangeId,
        long? PriceChangeSetId,
        IReadOnlyList<long> RefundedOrderServiceIds,
        decimal ApprovedRefundAmount,
        int CommercialVersion,
        long FinancialSequence,
        long ObligationVersion,
        decimal CustomerTotal,
        ProviderOperationOutcome DocumentRefundOutcome,
        ProviderOperationOutcome ValueMovementOutcome,
        ServicingOperationStatus OperationStatus,
        bool RefundNotAvailable,
        bool IsReplay);
}
