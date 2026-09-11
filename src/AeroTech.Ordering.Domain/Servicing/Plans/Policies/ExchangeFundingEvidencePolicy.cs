using AeroTech.Ordering.Domain.Ports.ExchangeFunding;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ExchangeFundingEvidencePolicy
    {
        public const string GuaranteeStage = "guarantee";
        public const string CaptureStage = "capture";

        public static string? GuaranteeContradiction(AcceptedExchangePlan plan, ExchangeFundingResult result)
            => Contradiction(plan, result, GuaranteeStage);

        public static string? CaptureContradiction(AcceptedExchangePlan plan, ExchangeFundingResult result)
            => Contradiction(plan, result, CaptureStage);

        private static string? Contradiction(AcceptedExchangePlan plan, ExchangeFundingResult result, string stage)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(result);

            if (plan.AddCollect is not { } obligation)
                return $"the {stage} confirms funding for an exchange that collects nothing";

            if (string.IsNullOrWhiteSpace(result.ProviderReference))
                return $"the confirmed {stage} carries no provider reference";

            if (result.Amount is not { } amount)
                return $"the confirmed {stage} carries no amount";

            if (result.CurrencyId is not { } currencyId)
                return $"the confirmed {stage} carries no currency";

            if (amount != obligation.Amount)
                return $"the confirmed {stage} moved {amount} against an obligation of {obligation.Amount}";

            return currencyId != obligation.CurrencyId
                ? $"the confirmed {stage} used currency {currencyId} against an obligation in {obligation.CurrencyId}"
                : null;
        }
    }
}
