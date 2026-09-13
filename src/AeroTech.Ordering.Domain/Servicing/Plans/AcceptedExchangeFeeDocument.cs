using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangeFeeDocument(
        string DocumentReference,
        string SourceReference,
        long IssuerCarrierId,
        long? TravelerId,
        string ReasonForIssuanceCode,
        int CurrencyId,
        decimal TotalAmount,
        IReadOnlyList<AcceptedServicingFeeDocumentCoupon> Coupons,
        string? AllocatedDocumentNumber = null,
        ProviderOperationOutcome? IssuanceOutcome = null,
        string? IssuanceProviderReference = null,
        string? IssuanceDetail = null,
        long? ElectronicMiscDocumentId = null,
        DateTimeOffset? SettledAt = null)
    {
        public const string LegPrefix = "emd-fee";

        public string LegIdentity => $"{LegPrefix}:{DocumentReference}";

        public string StockRole => $"Emd:{LegIdentity}";

        public bool IsAllocated => !string.IsNullOrWhiteSpace(AllocatedDocumentNumber);

        public bool IsIssuanceConfirmed => IssuanceOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsIssuanceRejected => IssuanceOutcome == ProviderOperationOutcome.Rejected;

        public bool IsSettled => SettledAt is not null;

        public ExchangeAncillaryState State => IsSettled
            ? ExchangeAncillaryState.Confirmed
            : IsIssuanceRejected
                ? ExchangeAncillaryState.Rejected
                : IssuanceOutcome is null && !IsAllocated
                    ? ExchangeAncillaryState.NotStarted
                    : ExchangeAncillaryState.Pending;
    }
}
