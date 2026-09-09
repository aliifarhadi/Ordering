namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public interface IElectronicMiscDocumentIssuer
    {
        Task<ElectronicMiscDocumentIssuanceOutcome> IssueAsync(
            ElectronicMiscDocumentIssuanceRequest request,
            CancellationToken cancellationToken = default);
    }
}
