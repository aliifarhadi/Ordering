using AeroTech.Ordering.Domain.OrderAggregate.Dto;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record RefundCorrectionArgs(
        long RefundRecordId,
        long OriginalRefundOperationId,
        long RefundPriceChangeSetId,
        IReadOnlyList<RestoredDocumentLink> RestoredLinks,
        long OperationId,
        string Reason,
        long? ActorId = null,
        string? ActorScope = null);
}
