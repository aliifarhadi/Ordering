using AeroTech.Ordering.Domain.Ports.ExchangeResidual;
using AeroTech.Ordering.Domain.Ports.RefundValue;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ExchangeSettlementEvidencePolicy
    {
        public static string? RefundDueContradiction(AcceptedExchangePlan plan, RefundValueResult result)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(result);

            if (plan.RefundDue is not { } obligation)
                return "the refund confirms value for an exchange that owes nothing";

            if (string.IsNullOrWhiteSpace(result.ValueMovementReference))
                return "the confirmed refund carries no value movement reference";

            if (result.Amount is not { } amount)
                return "the confirmed refund carries no amount";

            if (result.CurrencyId is not { } currencyId)
                return "the confirmed refund carries no currency";

            if (amount != obligation.Amount)
                return $"the confirmed refund returned {amount} against an obligation of {obligation.Amount}";

            if (currencyId != obligation.CurrencyId)
                return $"the confirmed refund used currency {currencyId} against an obligation in {obligation.CurrencyId}";

            return result.Disposition is { } disposition
                   && !string.Equals(disposition, obligation.Disposition, StringComparison.Ordinal)
                ? $"the confirmed refund used disposition {disposition} against an accepted {obligation.Disposition}"
                : null;
        }

        public static string? ResidualContradiction(AcceptedExchangePlan plan, ExchangeResidualResult result)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(result);

            if (plan.Residual is not { } obligation)
                return "the residual confirms value for an exchange that owes nothing";

            if (string.IsNullOrWhiteSpace(result.ProviderReference))
                return "the confirmed residual carries no provider reference";

            if (string.IsNullOrWhiteSpace(result.InstrumentReference))
                return "the confirmed residual identifies no value instrument";

            if (result.Instrument is null)
                return "the confirmed residual names no instrument family";

            if (result.Amount is not { } amount)
                return "the confirmed residual carries no amount";

            if (result.CurrencyId is not { } currencyId)
                return "the confirmed residual carries no currency";

            if (amount != obligation.Amount)
                return $"the confirmed residual issued {amount} against an obligation of {obligation.Amount}";

            return currencyId != obligation.CurrencyId
                ? $"the confirmed residual used currency {currencyId} against an obligation in {obligation.CurrencyId}"
                : null;
        }
    }
}
