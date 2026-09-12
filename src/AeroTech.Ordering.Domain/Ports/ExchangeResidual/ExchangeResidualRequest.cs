using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ExchangeResidual
{
    public sealed record ExchangeResidualRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string PredecessorDocumentNumber,
        string SuccessorDocumentNumber,
        long BeneficiaryTravellerId,
        decimal Amount,
        int CurrencyId,
        string Disposition,
        ResidualInstrumentKind ExpectedInstrument,
        string? SourcePricingReference);
}
