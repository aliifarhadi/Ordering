using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class ExchangePricingPolicy
    {
        public static string? DeferralReason(AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            return accepted.MonetaryOutcome switch
            {
                ChangeMonetaryOutcome.Even => EvenDeferralReason(accepted),
                ChangeMonetaryOutcome.AddCollect => null,
                _ => accepted.MonetaryOutcome.ToString()
            };
        }

        public static bool RequiresFunding(AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            return accepted.MonetaryOutcome == ChangeMonetaryOutcome.AddCollect;
        }

        private static string? EvenDeferralReason(AcceptedExchange accepted)
        {
            if (accepted.PricingLines.Any(line => line.ComponentType == PricingComponentType.Penalty))
                return nameof(PricingComponentType.Penalty);

            if (accepted.PricingLines.Any(line => line.ComponentType == PricingComponentType.Fee))
                return nameof(PricingComponentType.Fee);

            return NetCustomerBalance(accepted.PricingLines) == 0m ? null : "NonZeroCustomerBalance";
        }

        public static decimal NetCustomerBalance(IEnumerable<AcceptedExchangePricingLine> lines)
            => lines
                .Where(line => line.Effect == PricingEffect.CustomerBalance)
                .Sum(line => PricingComponentPolicy.Sign(line.Direction) * line.SaleAmount);

        public static void EnsureWellFormed(AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            if (accepted.PricingSource is PricingSource.OrderingDerived or PricingSource.Manual)
                throw Malformed(accepted, $"pricing source {accepted.PricingSource} is not an exchange pricing authority");

            EnsureCouponScopeIsWellFormed(accepted);
            EnsureTransferLinesAreWellFormed(accepted);
            EnsureSuccessorAttributionIsWellFormed(accepted);
            EnsureMonetaryOutcomeIsWellFormed(accepted);
        }

        private static void EnsureMonetaryOutcomeIsWellFormed(AcceptedExchange accepted)
        {
            var balance = NetCustomerBalance(accepted.PricingLines);

            if (accepted.MonetaryOutcome != ChangeMonetaryOutcome.AddCollect)
            {
                if (accepted.AddCollect is not null)
                    throw Malformed(accepted, $"a {accepted.MonetaryOutcome} outcome carries an add-collect amount");

                return;
            }

            if (accepted.AddCollect is not { } addCollect)
                throw Malformed(accepted, "an add-collect outcome carries no authoritative amount");

            if (addCollect.Amount <= 0m)
                throw Malformed(accepted, $"add-collect amount {addCollect.Amount} is not payable");

            if (addCollect.CurrencyId != accepted.SaleCurrencyId)
                throw Malformed(
                    accepted,
                    $"add-collect currency {addCollect.CurrencyId} is outside the sale currency {accepted.SaleCurrencyId}");

            if (balance != addCollect.Amount)
                throw Malformed(
                    accepted,
                    $"add-collect amount {addCollect.Amount} contradicts the customer balance {balance} of its pricing lines");
        }

        private static void EnsureCouponScopeIsWellFormed(AcceptedExchange accepted)
        {
            if (accepted.Coupons.Count == 0)
                throw Malformed(accepted, "no coupon dispositions");

            if (accepted.Coupons.Select(coupon => coupon.PredecessorTicketCouponId).Distinct().Count() != accepted.Coupons.Count
                || accepted.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Distinct().Count() != accepted.Coupons.Count)
                throw Malformed(accepted, "duplicate coupon disposition");

            if (accepted.Coupons.Any(coupon => coupon.IsReplaced != coupon.Replacement is not null))
                throw Malformed(accepted, "coupon disposition does not match its replacement");

            if (accepted.ChangedOrderServiceIds.Count == 0
                || accepted.ChangedOrderServiceIds.Distinct().Count() != accepted.ChangedOrderServiceIds.Count)
                throw Malformed(accepted, "changed services must be a non-empty distinct set");

            var replacedServices = accepted.Coupons
                .Where(coupon => coupon.IsReplaced)
                .Select(coupon => coupon.PredecessorOrderServiceId)
                .ToHashSet();

            if (!replacedServices.SetEquals(accepted.ChangedOrderServiceIds))
                throw Malformed(accepted, "replaced coupons do not cover the changed services");

            if (accepted.Coupons.Any(coupon => coupon.Successor.IssuanceValue < 0m))
                throw Malformed(accepted, "negative successor issuance value");
        }

        private static void EnsureTransferLinesAreWellFormed(AcceptedExchange accepted)
        {
            if (accepted.PricingLines.Count == 0)
                throw Malformed(accepted, "no pricing lines");

            if (accepted.PricingLines.All(line => line.LineRole != PricingLineRole.Transfer))
                throw Malformed(accepted, "no transfer line");

            if (accepted.PricingLines.Any(line =>
                    line.LineRole == PricingLineRole.Transfer && string.IsNullOrWhiteSpace(line.TransferGroupId)))
                throw Malformed(accepted, "transfer line without group");

            if (accepted.PricingLines.Any(line => string.IsNullOrWhiteSpace(line.SourceLineRef)))
                throw Malformed(accepted, "line without source identity");

            if (accepted.PricingLines.Select(line => line.SourceLineRef).Distinct(StringComparer.Ordinal).Count()
                != accepted.PricingLines.Count)
                throw Malformed(accepted, "duplicate source identity");
        }

        private static void EnsureSuccessorAttributionIsWellFormed(AcceptedExchange accepted)
        {
            var sourceRefs = accepted.PricingLines.Select(line => line.SourceLineRef).ToHashSet(StringComparer.Ordinal);
            var attributed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var coupon in accepted.Coupons)
            {
                if (coupon.Successor.PriceLinks.Count == 0)
                    throw Malformed(accepted, $"coupon {coupon.PredecessorCouponNumber} has no successor attribution");

                foreach (var link in coupon.Successor.PriceLinks)
                {
                    if (!sourceRefs.Contains(link.SourceLineRef))
                        throw ExceptionFactory.ExchangeSuccessorAttributionUnresolved(link.SourceLineRef);

                    if (link.CurrencyId != accepted.SaleCurrencyId)
                        throw Malformed(accepted, "successor attribution outside the sale currency");

                    if (!attributed.Add(link.SourceLineRef))
                        throw Malformed(accepted, "duplicate successor attribution");
                }
            }
        }

        private static Exception Malformed(AcceptedExchange accepted, string detail)
            => ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, detail);
    }
}
