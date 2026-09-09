using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility
{
    public interface IDocumentChangeEligibilityPort
    {
        Task<DocumentChangeEligibility> EvaluateAsync(
            DocumentChangeEligibilityRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record DocumentChangeEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long TicketCouponId,
        int CouponNumber,
        string TargetSelectionRef,
        ChangeMonetaryOutcome MonetaryOutcome);

    public sealed record DocumentChangeEligibility(
        DocumentChangeEligibilityOutcome Outcome,
        string? Detail = null);
}
