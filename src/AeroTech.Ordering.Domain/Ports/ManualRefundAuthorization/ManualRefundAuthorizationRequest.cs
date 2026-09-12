using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.Ports.ManualRefundAuthorization
{
    public sealed record ManualRefundAuthorizationRequest(
        long OrderId,
        long ElectronicTicketId,
        long OperationId,
        long ActorId,
        string ActorScope,
        BusinessContextType ContextType,
        AuthorizationSurface Surface,
        decimal ApprovedRefundAmount,
        int CurrencyId,
        string AuthorityReference,
        string Reason);
}
