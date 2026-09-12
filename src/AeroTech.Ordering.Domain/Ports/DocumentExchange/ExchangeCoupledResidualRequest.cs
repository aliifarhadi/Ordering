using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record ExchangeCoupledResidualRequest(
        decimal Amount,
        int CurrencyId,
        string Disposition,
        ResidualInstrumentKind ExpectedInstrument);
}
