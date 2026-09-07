using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.Messages.JetPay.AsyncCommands.V1
{
    public sealed record PaymentFactLine(
        PaymentFactComponentType ComponentType,
        decimal Amount,
        TenderType TenderType,
        long? ProviderProfileId,
        string? ProviderReference,
        string? CardScheme,
        string? Channel,
        string? ReferencesJson);

    // JetPay → Ledger request to post a payment accounting fact; Ledger replies with a Submit…Fact
    // acknowledgment. Type identity (namespace + name) is the contract — it must match JetPay's copy.
    //
    // The envelope (SourceSystem/EventId/CorrelationId/CausationId) is carried explicitly because BaseCommand
    // has none: SourceSystem is part of the A5 idempotency key, and EventId is the source-event id the ledger
    // posts under. Set via object initializer, mirroring how BaseIntegrationEvent-derived facts carry theirs.
    public sealed record SubmitPaymentFactCommand(
        long TenantId,
        long PaymentAccountingFactSetId,
        long PaymentIntentId,
        long OriginalOperationId,
        long OrderId,
        long IssuerLegalEntityId,
        PaymentAccountingFactType FactType,
        decimal TotalAmount,
        int CurrencyId,
        PaymentSettlementStatus SettlementStatus,
        DateTimeOffset OccurredAt,
        DateTimeOffset EffectiveAt,
        int FactVersion,
        string ContentHash,
        string Destination,
        IReadOnlyList<PaymentFactLine> Lines) : BaseCommand
    {
        public string SourceSystem { get; set; } = default!;

        public string EventId { get; set; } = default!;

        public string? CorrelationId { get; set; }

        public string? CausationId { get; set; }
    }
}
