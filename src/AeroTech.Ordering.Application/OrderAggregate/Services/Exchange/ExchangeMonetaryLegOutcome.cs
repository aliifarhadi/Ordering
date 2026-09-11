using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeMonetaryLegOutcome(
        ExchangeMonetaryLegKind Kind,
        string LegIdentity,
        decimal Amount,
        int CurrencyId,
        string? Disposition,
        ExchangeMonetaryState State,
        string? ProviderReference,
        string? InstrumentReference,
        ResidualInstrumentKind? Instrument);
}
