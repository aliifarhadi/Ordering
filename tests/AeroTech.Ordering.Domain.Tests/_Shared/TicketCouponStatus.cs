using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class TicketCouponStatus
    {
        public static void Fly(TicketCoupon coupon)
            => Set(coupon, TicketCouponFinancialStatus.Used);

        public static void Set(TicketCoupon coupon, TicketCouponFinancialStatus status)
            => typeof(TicketCoupon)
                .GetProperty(nameof(TicketCoupon.FinancialStatus), BindingFlags.Public | BindingFlags.Instance)!
                .SetValue(coupon, status);
    }
}
