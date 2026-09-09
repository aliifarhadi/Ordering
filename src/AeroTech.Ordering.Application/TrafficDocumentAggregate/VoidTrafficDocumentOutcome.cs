using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate
{
    public sealed record VoidTrafficDocumentOutcome(
        long DocumentId,
        TrafficDocumentStatus DocumentStatus,
        FulfillmentFailureReason? FailureReason,
        long VoidTaskId);
}
