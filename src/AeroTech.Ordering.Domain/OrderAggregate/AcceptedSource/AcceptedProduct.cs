using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedProduct(
        string ProductRef,
        string TravellerRef,
        ProductType ProductType,
        decimal Quantity,
        OrderItemUnitOfMeasure UnitOfMeasure,
        AcceptedProductSnapshot Snapshot,
        AcceptedCommercialTerms CommercialTerms,
        IReadOnlyList<AcceptedService> Services,
        string? ProductCode = null,
        string? ProductName = null);
}
