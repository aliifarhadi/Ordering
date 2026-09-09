using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record ElectronicTicketIssuanceRequest(
        Order Order,
        OrderOperation Operation,
        DocumentStock Stock,
        long OwnerAirlineId,
        IReadOnlyList<long> Scope,
        IReadOnlyCollection<long> Outstanding,
        IReadOnlyList<IssuedTicketSummary> AlreadyIssued);
}
