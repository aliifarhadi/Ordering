using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.Ports.CancelRefundAuthorization
{
    public interface ICancelRefundAuthorizationPort
    {
        Task<CancelRefundAuthorizationDecision> AuthorizeAsync(
            CancelRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default);
    }

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

    public sealed record CancelRefundAuthorizationDecision(
        ManualRefundAuthorizationOutcome Outcome,
        string? DecisionReference = null,
        string? Detail = null);
}
