using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record ResidualDocumentIdentity(
        string DocumentNumber,
        ResidualInstrumentKind Instrument,
        decimal Amount,
        int CurrencyId,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        string ReasonForIssuanceCode,
        string ReasonForIssuanceSubCode,
        string? ProviderReference = null);
}
