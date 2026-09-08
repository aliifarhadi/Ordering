using System.ComponentModel.DataAnnotations;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Query.OrderAggregate.View;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record OrderChangeRequest(
        int? ExpectedCommercialVersion,
        [property: Required] IReadOnlyList<AcceptSelectedQuotedOffer> AcceptSelectedQuotedOfferList);

    public sealed record AcceptSelectedQuotedOffer(
        [property: Required] string QuotedOfferId,
        [property: Required] IReadOnlyList<string> SelectedOfferItemIds);

    public sealed record OrderChangeResponse(long OperationId, int CommercialVersion, OrderView? Order);

    public static class OrderChangeRequestMapper
    {
        public static IReadOnlyList<SelectedQuotedOffer> ToSelections(OrderChangeRequest request)
            => (request.AcceptSelectedQuotedOfferList ?? [])
                .Select(selected => new SelectedQuotedOffer(selected.QuotedOfferId, selected.SelectedOfferItemIds ?? []))
                .ToList();
    }
}
