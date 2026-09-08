using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderFarePricingGroupArgs(
        long Id,
        IReadOnlyList<long> TravellerIds,
        PassengerTypeCode? PassengerType = null,
        string? SourceReference = null);
}
