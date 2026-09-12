using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.Ports.CancelRefundAuthorization
{
    public sealed record CancelRefundAuthorizationRequest(
        long OrderId,
        long ElectronicTicketId,
        long RefundRecordId,
        long OperationId,
        long OriginalRefundOperationId,
        long ActorId,
        string ActorScope,
        BusinessContextType ContextType,
        AuthorizationSurface Surface,
        decimal CorrectedAmount,
        int CurrencyId,
        string Reason,
        string? ReasonDetail);
}
