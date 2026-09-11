using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record FareConstructionGroupContext(
        PassengerTypeCode? PassengerType,
        string? SourceReference,
        IReadOnlyList<long> OrderTravellerIds,
        IReadOnlyList<FareConstructionUnitContext> Units);
}
