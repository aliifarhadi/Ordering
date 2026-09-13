using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record ServicingControlEvidence(
        long ElectronicTicketId,
        string DocumentNumber,
        int CouponNumber,
        TicketCouponControlStatus ControlStatus,
        TicketCouponFinancialStatus FinancialStatus)
    {
        public bool IsLocallyControlled => ControlStatus == TicketCouponControlStatus.Local;
    }
}
