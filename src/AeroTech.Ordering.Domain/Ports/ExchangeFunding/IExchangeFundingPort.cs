using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public interface IExchangeFundingPort
    {
        Task<ExchangeFundingResult> GuaranteeAsync(
            ExchangeFundingGuaranteeRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingRecovery> RecoverGuaranteeAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingResult> CaptureAsync(
            ExchangeFundingCaptureRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingRecovery> RecoverCaptureAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingResult> ReleaseAsync(
            ExchangeFundingReleaseRequest request,
            CancellationToken cancellationToken = default);

        Task<ExchangeFundingRecovery> RecoverReleaseAsync(
            ExchangeFundingRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record ExchangeFundingGuaranteeRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string PredecessorDocumentNumber,
        long PayerTravellerId,
        decimal Amount,
        int CurrencyId,
        string FundingMethodRef);

    public sealed record ExchangeFundingCaptureRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string SuccessorDocumentNumber,
        string? GuaranteeReference,
        decimal Amount,
        int CurrencyId);

    public sealed record ExchangeFundingReleaseRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string? GuaranteeReference,
        ExchangeFundingReleaseReason Reason);

    public sealed record ExchangeFundingRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);

    public sealed record ExchangeFundingResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Detail = null);

    public sealed record ExchangeFundingRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Detail = null)
    {
        public ExchangeFundingResult AsResult() => new(Outcome, ProviderReference, Amount, CurrencyId, Detail);
    }
}
