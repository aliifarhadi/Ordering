using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record ElectronicMiscDocumentIssuanceOutcome(
        ProviderOperationOutcome Outcome,
        IReadOnlyList<IssuedMiscellaneousDocumentSummary> Summaries,
        IReadOnlyCollection<long> Outstanding,
        bool AlreadyIrreversible,
        string? Detail);
}
