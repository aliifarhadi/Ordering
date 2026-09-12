using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility
{
    public sealed record DocumentChangeEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long TicketCouponId,
        int CouponNumber,
        string TargetSelectionRef,
        ChangeMonetaryOutcome MonetaryOutcome);
}
