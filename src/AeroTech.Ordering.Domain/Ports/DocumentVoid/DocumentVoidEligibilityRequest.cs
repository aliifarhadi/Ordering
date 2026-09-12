using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentVoid
{
    public sealed record DocumentVoidEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        AccountableDocumentKind DocumentKind,
        string DocumentNumber);
}
