using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ExchangeResidual
{
    public interface IExchangeResidualValuePort
    {
        Task<ExchangeResidualResult> FulfillAsync(
            ExchangeResidualRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeResidualRecovery> RecoverAsync(
            ExchangeResidualRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

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

    public sealed record ExchangeResidualRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);

    public sealed record ExchangeResidualResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? InstrumentReference = null,
        ResidualInstrumentKind? Instrument = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Detail = null);

    public sealed record ExchangeResidualRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? InstrumentReference = null,
        ResidualInstrumentKind? Instrument = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Detail = null)
    {
        public ExchangeResidualResult AsResult()
            => new(Outcome, ProviderReference, InstrumentReference, Instrument, Amount, CurrencyId, Detail);
    }
}
