using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public static class AcceptedExchangeMonetaryPlan
    {
        public static IReadOnlyList<AcceptedExchangeMonetaryLeg> MonetaryLegs(this AcceptedExchange accepted)
        {
            ArgumentNullException.ThrowIfNull(accepted);

            var legs = new List<AcceptedExchangeMonetaryLeg>(2);

            if (accepted.AddCollect is { } collection)
                legs.Add(new AcceptedExchangeMonetaryLeg(
                    ExchangeMonetaryLegKind.Collection, collection.Amount, collection.CurrencyId, null, null));

            if (accepted.RefundDue is { } refundDue)
                legs.Add(new AcceptedExchangeMonetaryLeg(
                    ExchangeMonetaryLegKind.RefundDue,
                    refundDue.Amount,
                    refundDue.CurrencyId,
                    refundDue.Disposition,
                    null));

            if (accepted.Residual is { } residual)
                legs.Add(new AcceptedExchangeMonetaryLeg(
                    ExchangeMonetaryLegKind.Residual,
                    residual.Amount,
                    residual.CurrencyId,
                    residual.Disposition,
                    residual.ExpectedInstrument));

            return legs;
        }
    }
}
