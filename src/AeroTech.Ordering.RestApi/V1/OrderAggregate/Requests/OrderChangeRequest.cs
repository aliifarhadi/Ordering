namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record OrderChangeRequest(
        int? ExpectedCommercialVersion,
        IReadOnlyList<AcceptSelectedQuotedOffer>? AcceptSelectedQuotedOfferList = null,
        CancelOrderItem? CancelOrderItem = null,
        RemoveOrderServices? RemoveOrderServices = null,
        AcceptQuotedChange? AcceptQuotedChange = null,
        AcceptExchange? AcceptExchange = null);
}
