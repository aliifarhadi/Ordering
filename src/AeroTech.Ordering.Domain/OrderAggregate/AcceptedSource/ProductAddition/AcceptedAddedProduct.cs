using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAddedProduct(
        string ProductRef,
        ProductType ProductType,
        decimal Quantity,
        OrderItemUnitOfMeasure UnitOfMeasure,
        AcceptedProductSnapshot Snapshot,
        AcceptedCommercialTerms CommercialTerms,
        IReadOnlyList<AcceptedAddedService> Services,
        string? ProductCode = null,
        string? ProductName = null);
}
