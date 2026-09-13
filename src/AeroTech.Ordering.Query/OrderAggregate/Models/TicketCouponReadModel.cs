using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class TicketCouponReadModel
    {
        public long Id { get; set; }

        public long TicketId { get; set; }

        public int CouponNumber { get; set; }

        public TicketCouponControlStatus ControlStatus { get; set; }

        public TicketCouponFinancialStatus FinancialStatus { get; set; }
    }
}
