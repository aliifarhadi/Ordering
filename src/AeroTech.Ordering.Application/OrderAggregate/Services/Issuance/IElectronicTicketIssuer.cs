namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public interface IElectronicTicketIssuer
    {
        Task<ElectronicTicketIssuanceOutcome> IssueAsync(
            ElectronicTicketIssuanceRequest request,
            CancellationToken cancellationToken = default);
    }
}
