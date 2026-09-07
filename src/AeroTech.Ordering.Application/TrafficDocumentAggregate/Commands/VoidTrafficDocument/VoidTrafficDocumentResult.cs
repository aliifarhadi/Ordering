using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument
{
    public sealed record VoidTrafficDocumentResult(
        long OrderId,
        long DocumentId,
        TrafficDocumentStatus DocumentStatus,
        FulfillmentFailureReason? FailureReason,
        long VoidTaskId);
}
