using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;

namespace AeroTech.Ordering.Domain.Ports.OrderChange
{
    public interface IOrderChangeQuoteProvider
    {
        Task<AcceptedAddServiceChange> AcceptSelectedQuotedOfferAsync(
            AcceptedQuotedOfferSelection selection,
            CancellationToken cancellationToken = default);
    }

    public sealed record AcceptedQuotedOfferSelection(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedOfferId,
        string SelectedOfferItemId,
        int SaleCurrencyId);
}
