namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public interface IIssueOrderService
    {
        Task<IssueOrderOutcome> IssueAsync(
            long orderId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }
}
