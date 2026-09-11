using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Policies
{
    public static class ExchangeCapabilityPolicy
    {
        public static IReadOnlyList<TicketCoupon> ReissueScope(ElectronicTicket predecessor)
        {
            ArgumentNullException.ThrowIfNull(predecessor);

            predecessor.EnsureDocumentCanBeExchanged();

            if (predecessor.Coupons.FirstOrDefault(IsOutsideTheSupportedStates) is { } unsupported)
                throw ExceptionFactory.ExchangeCouponStateNotSupported(
                    predecessor.DocumentNumber, unsupported.CouponNumber, unsupported.FinancialStatus);

            var reissuable = predecessor.Coupons
                .Where(coupon => coupon.FinancialStatus == TicketCouponFinancialStatus.Open)
                .OrderBy(coupon => coupon.CouponNumber)
                .ToList();

            return reissuable.Count > 0
                ? reissuable
                : throw ExceptionFactory.DocumentNotExchangeable(predecessor.DocumentNumber, predecessor.StatusSummary);
        }

        public static void EnsureEligible(ElectronicTicket predecessor, IReadOnlyList<ExchangeCouponScope> scope)
        {
            ArgumentNullException.ThrowIfNull(scope);

            var reissuable = ReissueScope(predecessor);
            var scoped = scope.Select(item => item.TicketCouponId).ToHashSet();

            if (scoped.Count != scope.Count
                || scoped.Count != reissuable.Count
                || reissuable.Any(coupon => !scoped.Contains(coupon.Id)))
                throw ExceptionFactory.ExchangeCouponScopeIncomplete(
                    predecessor.DocumentNumber, reissuable.Count, scope.Count);

            predecessor.EnsureCouponsCanBeExchanged(scope);
        }

        private static bool IsOutsideTheSupportedStates(TicketCoupon coupon)
            => coupon.FinancialStatus is not (TicketCouponFinancialStatus.Open or TicketCouponFinancialStatus.Used);
    }
}
