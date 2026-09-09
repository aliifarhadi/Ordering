using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record ElectronicMiscDocumentIssuanceRequest(
        Order Order,
        OrderOperation Operation,
        DocumentStock Stock,
        long OwnerAirlineId,
        IReadOnlyList<long> Scope,
        IReadOnlyList<ElectronicTicket> Tickets,
        IReadOnlyList<IssuedMiscellaneousDocumentSummary> AlreadyIssued,
        bool AlreadyIrreversible);
}
