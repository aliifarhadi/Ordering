using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record ElectronicTicketIssuanceOutcome(
        ProviderOperationOutcome Outcome,
        IReadOnlyList<IssuedTicketSummary> Summaries,
        IReadOnlyCollection<long> Outstanding,
        bool AlreadyIrreversible,
        string? Detail);
}
