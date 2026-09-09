namespace AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange
{
    public interface IOrderChangeService
    {
        Task<OrderChangeOutcome> AddServiceAsync(
            long orderId,
            IReadOnlyList<SelectedQuotedOffer> acceptSelectedQuotedOfferList,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }
}
