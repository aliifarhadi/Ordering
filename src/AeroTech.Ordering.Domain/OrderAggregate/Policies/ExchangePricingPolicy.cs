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
                ChangeMonetaryOutcome.Refund => null,
                ChangeMonetaryOutcome.Residual => null,
                ChangeMonetaryOutcome.Mixed => null,
                _ => accepted.MonetaryOutcome.ToString()
            };
        }

        public static bool RequiresFunding(AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            return accepted.AddCollect is not null;
        }

        public static bool RequiresRefundDue(AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            return accepted.RefundDue is not null;
        }

        public static bool RequiresResidual(AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            return accepted.Residual is not null;
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

        public static void EnsureResidualFulfillmentIsCoherent(AcceptedExchange accepted, string quotedExchangeId)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            if (accepted.Residual is { IsDocumentCoupled: true } residual
                && residual.ExpectedInstrument != ResidualInstrumentKind.Emd)
                throw ExceptionFactory.ExchangeResidualFulfillmentMalformed(
                    quotedExchangeId, residual.ExpectedInstrument);
        }

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
            EnsureTheOutcomeMatchesItsLegs(accepted);
            EnsureLegsAreDistinct(accepted);

            switch (accepted.MonetaryOutcome)
            {
                case ChangeMonetaryOutcome.AddCollect:
                    EnsureCollectionIsWellFormed(accepted, 1);
                    break;

                case ChangeMonetaryOutcome.Refund:
                    EnsureRefundDueIsWellFormed(accepted, -1);
                    break;

                case ChangeMonetaryOutcome.Residual:
                    EnsureResidualIsWellFormed(accepted, -1);
                    break;

                case ChangeMonetaryOutcome.Mixed:
                    EnsureCollectionIsWellFormed(accepted, null);

                    if (accepted.RefundDue is not null)
                        EnsureRefundDueIsWellFormed(accepted, null);
                    else
                        EnsureResidualIsWellFormed(accepted, null);

                    break;
            }
        }

        private static void EnsureCollectionIsWellFormed(AcceptedExchange accepted, int? balanceSign)
            => EnsureSettlementIsWellFormed(
                accepted, "add-collect", accepted.AddCollect?.Amount, accepted.AddCollect?.CurrencyId, balanceSign);

        private static void EnsureRefundDueIsWellFormed(AcceptedExchange accepted, int? balanceSign)
        {
            EnsureSettlementIsWellFormed(
                accepted, "refund-due", accepted.RefundDue?.Amount, accepted.RefundDue?.CurrencyId, balanceSign);

            if (accepted.RefundDue is { IsOriginalRefundableSource: false } refundDue)
                throw Malformed(
                    accepted,
                    $"refund-due disposition {refundDue.Disposition} is not an original refundable source");
        }

        private static void EnsureResidualIsWellFormed(AcceptedExchange accepted, int? balanceSign)
        {
            EnsureSettlementIsWellFormed(
                accepted, "residual", accepted.Residual?.Amount, accepted.Residual?.CurrencyId, balanceSign);

            if (accepted.Residual is { Disposition: var disposition } && string.IsNullOrWhiteSpace(disposition))
                throw Malformed(accepted, "a residual outcome carries no authoritative disposition");
        }

        private static void EnsureTheOutcomeMatchesItsLegs(AcceptedExchange accepted)
        {
            var legs = accepted.MonetaryLegs();
            var collections = legs.Count(leg => leg.IsCollection);
            var returns = legs.Count(leg => leg.IsReturnOfValue);

            switch (accepted.MonetaryOutcome)
            {
                case ChangeMonetaryOutcome.Even when legs.Count > 0:
                    throw Malformed(accepted, "an even outcome carries monetary legs");

                case ChangeMonetaryOutcome.AddCollect when legs.Count != 1 || collections != 1:
                    throw Malformed(accepted, $"an add-collect outcome carries {Describe(legs)}");

                case ChangeMonetaryOutcome.Refund when legs.Count != 1 || accepted.RefundDue is null:
                    throw Malformed(accepted, $"a refund-due outcome carries {Describe(legs)}");

                case ChangeMonetaryOutcome.Residual when legs.Count != 1 || accepted.Residual is null:
                    throw Malformed(accepted, $"a residual outcome carries {Describe(legs)}");

                case ChangeMonetaryOutcome.Mixed when legs.Count != 2 || collections != 1 || returns != 1:
                    throw Malformed(
                        accepted,
                        $"a mixed outcome must collect once and return value once but carries {Describe(legs)}");
            }
        }

        private static void EnsureLegsAreDistinct(AcceptedExchange accepted)
        {
            var legs = accepted.MonetaryLegs();

            if (legs.Select(leg => leg.LegIdentity).Distinct(StringComparer.Ordinal).Count() != legs.Count
                || legs.Select(leg => leg.Kind).Distinct().Count() != legs.Count)
                throw Malformed(accepted, "duplicate monetary leg identity");
        }

        private static string Describe(IReadOnlyList<AcceptedExchangeMonetaryLeg> legs)
            => legs.Count == 0 ? "no monetary leg" : string.Join(" and ", legs.Select(leg => leg.LegIdentity));

        private static void EnsureSettlementIsWellFormed(
            AcceptedExchange accepted,
            string settlement,
            decimal? amount,
            int? currencyId,
            int? balanceSign)
        {
            if (amount is not { } settled)
                throw Malformed(accepted, $"a {settlement} outcome carries no authoritative amount");

            if (settled <= 0m)
                throw Malformed(accepted, $"{settlement} amount {settled} is not settleable");

            if (currencyId != accepted.SaleCurrencyId)
                throw Malformed(
                    accepted,
                    $"{settlement} currency {currencyId} is outside the sale currency {accepted.SaleCurrencyId}");

            if (balanceSign is not { } sign)
                return;

            var balance = NetCustomerBalance(accepted.PricingLines);

            if (balance != sign * settled)
                throw Malformed(
                    accepted,
                    $"{settlement} amount {settled} contradicts the customer balance {balance} of its pricing lines");
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
