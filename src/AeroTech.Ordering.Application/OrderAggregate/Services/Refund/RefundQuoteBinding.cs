using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public static class RefundQuoteBinding
    {
        public static void EnsureQuoteBindsToTheOrder(
            RefundQuote quote,
            Order order,
            ElectronicTicket ticket,
            IReadOnlyCollection<long> ticketCouponIds)
        {
            EnsurePricingAuthorityIsExternal(quote.PricingSource);
            EnsureAmountIsWellFormed(quote.ApprovedRefundAmount);
            RefundConservationPolicy.EnsureReconciles(quote.PricingLines, quote.ApprovedRefundAmount);

            if (quote.OrderId != order.Id)
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("order");

            if (quote.ElectronicTicketId != ticket.Id)
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("document");

            if (quote.SaleCurrencyId != order.CurrencyId)
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("sale currency");

            EnsureCouponScopeMatches(quote.TicketCouponIds, ticketCouponIds);
        }

        public static void EnsureAcceptedBindsToTheRequest(
            AcceptedRefund accepted,
            Order order,
            ElectronicTicket ticket,
            IReadOnlyCollection<long> ticketCouponIds,
            string quotedRefundId,
            int expectedCommercialVersion,
            DateTimeOffset now)
        {
            EnsurePricingAuthorityIsExternal(accepted.PricingSource);
            EnsureAmountIsWellFormed(accepted.ApprovedRefundAmount);

            if (!string.Equals(accepted.QuotedRefundId, quotedRefundId, StringComparison.Ordinal))
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("quoted refund identity");

            if (accepted.OrderId != order.Id)
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("order");

            if (accepted.ExpectedCommercialVersion != expectedCommercialVersion)
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("commercial version");

            if (accepted.ElectronicTicketId != ticket.Id)
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("document");

            if (accepted.SaleCurrencyId != order.CurrencyId)
                throw ExceptionFactory.AcceptedRefundDoesNotMatchTheRequest("sale currency");

            if (accepted.PricingLines.Count == 0)
                throw ExceptionFactory.RefundRequiresPricingLines(accepted.QuotedRefundId);

            if (accepted.ExpiresAt <= now)
                throw ExceptionFactory.RefundQuoteExpired(accepted.QuotedRefundId, accepted.ExpiresAt);

            EnsureCouponScopeMatches(accepted.TicketCouponIds, ticketCouponIds);
        }

        private static void EnsurePricingAuthorityIsExternal(PricingSource source)
        {
            if (source == PricingSource.OrderingDerived)
                throw ExceptionFactory.RefundPricingSourceNotAllowed(source);
        }

        private static void EnsureAmountIsWellFormed(decimal approvedRefundAmount)
        {
            if (approvedRefundAmount < 0m)
                throw ExceptionFactory.RefundAmountMustBeNonNegative(approvedRefundAmount);
        }

        private static void EnsureCouponScopeMatches(
            IReadOnlyList<long> quoted,
            IReadOnlyCollection<long> ticketCouponIds)
        {
            if (!quoted.Distinct().ToHashSet().SetEquals(ticketCouponIds))
                throw ExceptionFactory.AcceptedRefundScopeMismatch();
        }
    }
}
