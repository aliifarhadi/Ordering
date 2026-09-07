namespace AeroTech.Messages.StoredValue.AsyncCommands.V1
{
    public sealed record WalletAccountingFactLine(
        string FundClass,
        decimal Amount,
        string AccountingClassification,
        string RegulatoryClassification,
        long IssuerLegalEntityId,
        string? ReferencesJson);

    // StoredValue → Ledger request to post a wallet accounting fact; the messaging envelope
    // (EventId/SourceSystem/CorrelationId/CausationId/TenantId/TimeOfOccurrence) is inherited from BaseCommand.
    public sealed record SubmitWalletFactCommand(
        long AccountingFactSetId,
        long WalletTransactionId,
        string FactType,
        decimal TotalAmount,
        int CurrencyId,
        DateTimeOffset OccurredAt,
        DateTimeOffset EffectiveAt,
        int FactVersion,
        string ContentHash,
        string Destination,
        IReadOnlyList<WalletAccountingFactLine> Lines) : BaseCommand;
}
