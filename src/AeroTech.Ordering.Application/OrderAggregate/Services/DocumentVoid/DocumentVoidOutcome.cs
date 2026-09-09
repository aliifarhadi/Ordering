using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.DocumentVoid
{
    public sealed record DocumentVoidOutcome(
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
