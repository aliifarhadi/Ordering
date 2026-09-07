using AeroTech.Messages.LedgerFlow.Enums;

namespace AeroTech.Messages.LedgerFlow.IntegrationEvents.V1
{
    public record JournalPosted(
        string EventId,
        string AggregateId,
        DateTimeOffset TimeOfOccurrence,
        long TenantId,
        long JournalEntryId,
        long BookId,
        long LegalEntityId,
        JournalType Type,
        DateOnly AccountingDate,
        int FunctionalCurrencyId,
        decimal DebitTotal,
        decimal CreditTotal);
}
