using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record AcceptSelectedQuotedOffer(
        [property: Required] string QuotedOfferId,
        [property: Required] IReadOnlyList<string> SelectedOfferItemIds);
}
