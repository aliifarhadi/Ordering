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

            if (accepted.MonetaryOutcome != ChangeMonetaryOutcome.Even)
                return accepted.MonetaryOutcome.ToString();

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
                throw ExceptionFactory.ExchangePricingMalformed(
                    accepted.QuotedExchangeId, $"pricing source {accepted.PricingSource} is not an exchange pricing authority");

            if (accepted.PricingLines.Count == 0)
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "no pricing lines");

            if (accepted.PricingLines.All(line => line.LineRole != PricingLineRole.Transfer))
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "no transfer line");

            if (accepted.PricingLines.Any(line =>
                    line.LineRole == PricingLineRole.Transfer && string.IsNullOrWhiteSpace(line.TransferGroupId)))
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "transfer line without group");

            if (accepted.PricingLines.Any(line => string.IsNullOrWhiteSpace(line.SourceLineRef)))
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "line without source identity");

            if (accepted.PricingLines.Select(line => line.SourceLineRef).Distinct(StringComparer.Ordinal).Count()
                != accepted.PricingLines.Count)
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "duplicate source identity");

            var sourceRefs = accepted.PricingLines.Select(line => line.SourceLineRef).ToHashSet(StringComparer.Ordinal);

            foreach (var link in accepted.SuccessorCoupon.PriceLinks)
            {
                if (!sourceRefs.Contains(link.SourceLineRef))
                    throw ExceptionFactory.ExchangeSuccessorAttributionUnresolved(link.SourceLineRef);

                if (link.CurrencyId != accepted.SaleCurrencyId)
                    throw ExceptionFactory.ExchangePricingMalformed(
                        accepted.QuotedExchangeId, "successor attribution outside the sale currency");
            }

            if (accepted.SuccessorCoupon.PriceLinks.Select(link => link.SourceLineRef).Distinct(StringComparer.Ordinal).Count()
                != accepted.SuccessorCoupon.PriceLinks.Count)
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "duplicate successor attribution");

            if (accepted.SuccessorCoupon.PriceLinks.Count == 0)
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "no successor attribution");

            if (accepted.SuccessorCoupon.IssuanceValue < 0m)
                throw ExceptionFactory.ExchangePricingMalformed(accepted.QuotedExchangeId, "negative successor issuance value");
        }
    }
}
