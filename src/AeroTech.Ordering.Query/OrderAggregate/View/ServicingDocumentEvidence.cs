using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record ServicingDocumentEvidence(
        AccountableDocumentKind Kind,
        long DocumentId,
        string DocumentNumber,
        string StatusSummary,
        int DocumentVersion,
        long? PredecessorElectronicTicketId,
        string? ProviderReference);
}
