using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record IssueOrderOutcome(
        long OrderId,
        long OperationId,
        long ReceiptId,
        ProviderOperationOutcome Outcome,
        ServicingOperationStatus OperationStatus,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        IReadOnlyList<IssuedTicketSummary> Tickets,
        IReadOnlyList<long> OutstandingServiceIds,
        string? Detail,
        IReadOnlyList<IssuedMiscellaneousDocumentSummary> MiscellaneousDocuments);
}
