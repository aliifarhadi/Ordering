using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal static class ExchangeMonetaryLegAssertions
    {
        public static bool IsCollectionLeg(this ExchangeMonetaryLegOutcome leg)
            => leg.Kind == ExchangeMonetaryLegKind.Collection;

        public static bool IsReturnLeg(this ExchangeMonetaryLegOutcome leg)
            => leg.Kind is ExchangeMonetaryLegKind.RefundDue or ExchangeMonetaryLegKind.Residual;
    }
}
