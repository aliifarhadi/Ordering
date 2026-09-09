using System.ComponentModel.DataAnnotations;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Query.OrderAggregate.View;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record OrderChangeRequest(
        int? ExpectedCommercialVersion,
        IReadOnlyList<AcceptSelectedQuotedOffer>? AcceptSelectedQuotedOfferList = null,
        CancelOrderItem? CancelOrderItem = null,
        RemoveOrderServices? RemoveOrderServices = null);

    public sealed record AcceptSelectedQuotedOffer(
        [property: Required] string QuotedOfferId,
        [property: Required] IReadOnlyList<string> SelectedOfferItemIds);

    public sealed record CancelOrderItem(
        long OrderItemId,
        [property: Required] string QuotedCancellationId);

    public sealed record RemoveOrderServices(
        [property: Required] IReadOnlyList<long> OrderServiceIds,
        [property: Required] string QuotedCancellationId);

    public sealed record OrderChangeResponse(long OperationId, int CommercialVersion, OrderView? Order);

    public enum OrderChangeVariant
    {
        AddService = 1,
        CancelOrderItem = 2,
        RemoveOrderServices = 3
    }

    public static class OrderChangeRequestMapper
    {
        public static IReadOnlyList<SelectedQuotedOffer> ToSelections(OrderChangeRequest request)
            => (request.AcceptSelectedQuotedOfferList ?? [])
                .Select(selected => new SelectedQuotedOffer(selected.QuotedOfferId, selected.SelectedOfferItemIds ?? []))
                .ToList();

        public static OrderChangeVariant ResolveVariant(OrderChangeRequest request)
        {
            var variants = new List<OrderChangeVariant>();

            if (request.AcceptSelectedQuotedOfferList is { Count: > 0 })
                variants.Add(OrderChangeVariant.AddService);

            if (request.CancelOrderItem is not null)
                variants.Add(OrderChangeVariant.CancelOrderItem);

            if (request.RemoveOrderServices is not null)
                variants.Add(OrderChangeVariant.RemoveOrderServices);

            return variants.Count == 1
                ? variants[0]
                : throw Domain._Shared.Resources.ExceptionFactory.OrderChangeVariantIsAmbiguous(variants.Count);
        }
    }
}
