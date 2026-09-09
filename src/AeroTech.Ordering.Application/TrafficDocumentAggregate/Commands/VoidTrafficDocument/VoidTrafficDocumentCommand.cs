using AeroTech.Messages.Ordering.Enums;
using MediatR;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument
{
    public sealed record VoidTrafficDocumentCommand(
        long OrderId,
        long DocumentId,
        VoidReason Reason,
        string? ReasonDetail,
        string IdempotencyKey) : IRequest<VoidTrafficDocumentResult>;
}
