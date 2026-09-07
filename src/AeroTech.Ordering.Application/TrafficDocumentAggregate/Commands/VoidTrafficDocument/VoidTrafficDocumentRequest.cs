using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument
{
    public sealed record VoidTrafficDocumentRequest(
        VoidReason Reason,
        string? ReasonDetail);
}
