using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
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

            if (request.AcceptQuotedChange is not null)
                variants.Add(OrderChangeVariant.AcceptQuotedChange);

            if (request.AcceptExchange is not null)
                variants.Add(OrderChangeVariant.AcceptExchange);

            return variants.Count == 1
                ? variants[0]
                : throw ExceptionFactory.OrderChangeVariantIsAmbiguous(variants.Count);
        }
    }
}
