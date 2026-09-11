using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedExchangeMonetaryLeg(
        ExchangeMonetaryLegKind Kind,
        decimal Amount,
        int CurrencyId,
        string? Disposition,
        ResidualInstrumentKind? ExpectedInstrument)
    {
        public const string CollectionIdentity = "collection";
        public const string RefundDueIdentity = "refund-due";
        public const string ResidualIdentity = "residual";

        public string LegIdentity => Kind switch
        {
            ExchangeMonetaryLegKind.Collection => CollectionIdentity,
            ExchangeMonetaryLegKind.RefundDue => RefundDueIdentity,
            _ => ResidualIdentity
        };

        public bool IsCollection => Kind == ExchangeMonetaryLegKind.Collection;

        public bool IsReturnOfValue => Kind is ExchangeMonetaryLegKind.RefundDue or ExchangeMonetaryLegKind.Residual;
    }
}
