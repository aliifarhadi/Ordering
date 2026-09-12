using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ExchangeResidual
{
    public sealed record ExchangeResidualResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? InstrumentReference = null,
        ResidualInstrumentKind? Instrument = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Detail = null);
}
