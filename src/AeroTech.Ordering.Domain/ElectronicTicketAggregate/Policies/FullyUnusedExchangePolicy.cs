using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Policies
{
    public static class FullyUnusedExchangePolicy
    {
        public static void EnsureEligible(ElectronicTicket predecessor, IReadOnlyList<ExchangeCouponScope> scope)
        {
            ArgumentNullException.ThrowIfNull(predecessor);
            ArgumentNullException.ThrowIfNull(scope);

            predecessor.EnsureDocumentCanBeExchanged();

            if (predecessor.FirstUsedCoupon() is { } used)
                throw ExceptionFactory.ExchangeRequiresFullyUnusedTicket(
                    predecessor.DocumentNumber, used.CouponNumber, used.FinancialStatus);

            var scoped = scope.Select(item => item.TicketCouponId).ToHashSet();

            if (scoped.Count != scope.Count
                || scoped.Count != predecessor.Coupons.Count
                || predecessor.Coupons.Any(coupon => !scoped.Contains(coupon.Id)))
                throw ExceptionFactory.ExchangeCouponScopeIncomplete(
                    predecessor.DocumentNumber, predecessor.Coupons.Count, scope.Count);

            predecessor.EnsureCouponsCanBeExchanged(scope);
        }
    }
}
