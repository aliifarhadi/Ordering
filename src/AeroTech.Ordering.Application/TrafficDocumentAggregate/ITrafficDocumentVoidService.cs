using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate
{
    public interface ITrafficDocumentVoidService
    {
        Task<VoidTrafficDocumentOutcome> VoidAsync(
            long orderId,
            long documentId,
            VoidReason reason,
            string? reasonDetail,
            long voidedBy,
            CancellationToken cancellationToken = default);
    }

    public sealed record VoidTrafficDocumentOutcome(
        long DocumentId,
        TrafficDocumentStatus DocumentStatus,
        FulfillmentFailureReason? FailureReason,
        long VoidTaskId);
}
