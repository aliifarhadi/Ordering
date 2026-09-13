using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanFeeDocumentRow
    {
        public long OperationId { get; set; }

        public string DocumentReference { get; set; } = null!;

        public string SourceReference { get; set; } = null!;

        public long IssuerCarrierId { get; set; }

        public long? TravelerId { get; set; }

        public string ReasonForIssuanceCode { get; set; } = null!;

        public int CurrencyId { get; set; }

        public decimal TotalAmount { get; set; }

        public string Coupons { get; set; } = null!;

        public string? AllocatedDocumentNumber { get; set; }

        public ProviderOperationOutcome? IssuanceOutcome { get; set; }

        public string? IssuanceProviderReference { get; set; }

        public string? IssuanceDetail { get; set; }

        public long? ElectronicMiscDocumentId { get; set; }

        public DateTimeOffset? SettledAt { get; set; }
    }
}
