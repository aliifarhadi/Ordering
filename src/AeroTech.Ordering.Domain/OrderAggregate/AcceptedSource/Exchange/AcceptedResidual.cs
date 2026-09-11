using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedResidual(
        decimal Amount,
        int CurrencyId,
        string Disposition,
        ResidualInstrumentKind ExpectedInstrument = ResidualInstrumentKind.Unknown);
}
