using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public IReadOnlyList<FareConstructionContext> FareConstructionContexts()
            => CurrentFareConstructions()
                .OrderBy(construction => construction.Id)
                .Select(Describe)
                .ToList();

        private static FareConstructionContext Describe(OrderAirFareConstruction construction)
            => new(
                construction.SourceSystem,
                construction.SourcePricingReference,
                construction.ConstructionType,
                construction.PricingGroups
                    .OrderBy(group => group.Id)
                    .Select(Describe)
                    .ToList());

        private static FareConstructionGroupContext Describe(OrderFarePricingGroup group)
            => new(
                group.PassengerType,
                group.SourceReference,
                group.Travellers.Select(traveller => traveller.OrderTravellerId).Order().ToList(),
                group.PricingUnits
                    .OrderBy(unit => unit.Sequence)
                    .Select(Describe)
                    .ToList());

        private static FareConstructionUnitContext Describe(OrderFarePricingUnit unit)
            => new(
                unit.Sequence,
                unit.PricingUnitType,
                unit.CombinationMethod,
                unit.SourceReference,
                unit.FareComponents
                    .OrderBy(component => component.Sequence)
                    .Select(Describe)
                    .ToList());

        private static FareConstructionComponentContext Describe(OrderFareComponent component)
            => new(
                component.Sequence,
                component.OriginAirportId,
                component.DestinationAirportId,
                component.FareBasis,
                component.BrandCode,
                component.FareType,
                component.CabinClassId,
                component.RbdId,
                component.BookingClass,
                component.FareOwnerCarrierId,
                component.TariffReference,
                component.RuleReference,
                component.RoutingReference,
                component.SourceFareReference,
                component.SourceComponentReference,
                component.Services.Select(service => service.OrderServiceId).Order().ToList(),
                component.Segments.Select(segment => segment.OrderSegmentId).Order().ToList());
    }
}
