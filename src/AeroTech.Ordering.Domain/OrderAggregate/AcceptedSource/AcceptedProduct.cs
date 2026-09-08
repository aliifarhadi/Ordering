using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedProduct(
        string ProductRef,
        string TravellerRef,
        ProductType ProductType,
        string ProductCode,
        string ProductName,
        decimal Quantity,
        OrderItemUnitOfMeasure UnitOfMeasure,
        AcceptedProductSnapshot Snapshot,
        AcceptedCommercialTerms CommercialTerms,
        IReadOnlyList<AcceptedService> Services);
}
