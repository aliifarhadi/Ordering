using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public sealed record CancelRefundOutcome(
        long OrderId,
        long OperationId,
        long ElectronicTicketId,
        string DocumentNumber,
        int DocumentVersion,
        ElectronicTicketStatus DocumentStatus,
        long RefundRecordId,
        long OriginalRefundOperationId,
        long? CorrectionRecordId,
        long? OrderChangeId,
        long? PriceChangeSetId,
        IReadOnlyList<long> RestoredTicketCouponIds,
        IReadOnlyList<long> RestoredOrderServiceIds,
        decimal CorrectedAmount,
        int CommercialVersion,
        long FinancialSequence,
        long ObligationVersion,
        decimal CustomerTotal,
        ProviderOperationOutcome DocumentCorrectionOutcome,
        ProviderOperationOutcome ValueCorrectionOutcome,
        ServicingOperationStatus OperationStatus,
        bool CorrectionNotAvailable,
        bool IsReplay);
}
