using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument
{
    public sealed record VoidTrafficDocumentResult(
        long OrderId,
        long OperationId,
        AccountableDocumentKind DocumentKind,
        long DocumentId,
        string DocumentNumber,
        int DocumentVersion,
        IReadOnlyList<long> AffectedOrderServiceIds,
        ProviderOperationOutcome ProviderOutcome,
        ServicingOperationStatus OperationStatus,
        bool RefundRequiredInstead,
        bool IsReplay);
}
