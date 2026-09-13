using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation
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
