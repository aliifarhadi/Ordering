using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.Ports.ManualRefundAuthorization
{
    public interface IManualRefundAuthorizationPort
    {
        Task<ManualRefundAuthorizationDecision> AuthorizeAsync(
            ManualRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default);
    }

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

    public sealed record ManualRefundAuthorizationDecision(
        ManualRefundAuthorizationOutcome Outcome,
        string? DecisionReference = null,
        string? Detail = null);
}
