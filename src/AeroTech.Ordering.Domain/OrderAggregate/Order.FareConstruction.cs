using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public IReadOnlyCollection<OrderAirFareConstruction> CurrentFareConstructions()
        {
            var superseded = _fareConstructions
                .Where(construction => construction.SupersedesConstructionId.HasValue)
                .Select(construction => construction.SupersedesConstructionId!.Value)
                .ToHashSet();

            return _fareConstructions
                .Where(construction => !superseded.Contains(construction.Id))
                .ToList();
        }

        public OrderFareComponent? ActiveFareComponentFor(long orderServiceId)
        {
            var covering = CurrentFareConstructions()
                .SelectMany(construction => construction.FareComponents)
                .Where(component => component.Covers(orderServiceId))
                .ToList();

            return covering.Count switch
            {
                0 => null,
                1 => covering[0],
                _ => throw ExceptionFactory.AmbiguousActiveFareComponent(orderServiceId, covering.Count)
            };
        }

        public string? ResolveIssueFareBasis(long orderServiceId)
        {
            if (ActiveFareComponentFor(orderServiceId) is { FareBasis: { } fareBasis })
                return fareBasis;

            return _orderServices
                .FirstOrDefault(service => service.Id == orderServiceId)
                ?.AirTransportDetail?.TransitionalFareBasis;
        }

        internal void AcceptFareConstructions(
            AcceptedOrderSource source,
            long changeId,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            foreach (var accepted in source.FareConstructions ?? [])
                AcceptFareConstruction(accepted, changeId, refs, idGenerator, now);
        }

        private void AcceptFareConstruction(
            AcceptedFareConstruction accepted,
            long changeId,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            long? supersedesConstructionId = null;

            if (accepted.SupersedesConstructionRef is { } supersededRef)
            {
                if (!refs.FareConstructionIds.TryGetValue(supersededRef, out var supersededId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("superseded fare construction", supersededRef);

                supersedesConstructionId = supersededId;
            }

            var construction = new OrderAirFareConstruction(new CreateOrderAirFareConstructionArgs(
                idGenerator.NewId(),
                Id,
                changeId,
                accepted.SourceSystem,
                now,
                accepted.ConstructionType,
                accepted.SourcePricingReference,
                supersedesConstructionId));

            foreach (var productRef in accepted.ProductRefs)
            {
                if (!refs.ProductItemIds.TryGetValue(productRef, out var orderItemId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("fare construction product", productRef);

                construction.CoverItem(orderItemId, idGenerator);
            }

            foreach (var acceptedGroup in accepted.PricingGroups)
                AcceptPricingGroup(construction, acceptedGroup, refs, idGenerator, now);

            EnsureFareConstructionIsCoherent(construction);

            _fareConstructions.Add(construction);
            refs.FareConstructionIds[accepted.ConstructionRef] = construction.Id;
        }

        private void AcceptPricingGroup(
            OrderAirFareConstruction construction,
            AcceptedFarePricingGroup accepted,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            if (accepted.TravellerRefs.Count == 0)
                throw ExceptionFactory.FarePricingGroupRequiresTraveller();

            var travellerIds = new List<long>();

            foreach (var travellerRef in accepted.TravellerRefs)
            {
                if (!refs.TravellerIds.TryGetValue(travellerRef, out var travellerId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("fare pricing group traveller", travellerRef);

                travellerIds.Add(travellerId);
            }

            var group = construction.AddPricingGroup(new CreateOrderFarePricingGroupArgs(
                idGenerator.NewId(),
                travellerIds,
                accepted.PassengerTypeCode,
                accepted.SourceReference), idGenerator);

            foreach (var acceptedUnit in accepted.PricingUnits)
            {
                var unit = group.AddPricingUnit(new CreateOrderFarePricingUnitArgs(
                    idGenerator.NewId(),
                    acceptedUnit.Sequence,
                    acceptedUnit.PricingUnitType,
                    acceptedUnit.CombinationMethod,
                    acceptedUnit.SourceReference), idGenerator);

                foreach (var acceptedComponent in acceptedUnit.FareComponents)
                    AcceptFareComponent(unit, acceptedComponent, refs, idGenerator, now);
            }
        }

        private void AcceptFareComponent(
            OrderFarePricingUnit unit,
            AcceptedFareComponent accepted,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            if (accepted.ServiceRefs.Count == 0)
                throw ExceptionFactory.FareComponentRequiresService();

            var serviceIds = new List<long>();

            foreach (var serviceRef in accepted.ServiceRefs)
            {
                if (!refs.ServiceIds.TryGetValue(serviceRef, out var serviceId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("fare component service", serviceRef);

                serviceIds.Add(serviceId);
            }

            var segmentIds = new List<long>();

            foreach (var segmentRef in accepted.SegmentRefs)
            {
                if (!refs.SegmentIds.TryGetValue(segmentRef, out var segmentId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("fare component segment", segmentRef);

                segmentIds.Add(segmentId);
            }

            unit.AddFareComponent(new CreateOrderFareComponentArgs(
                idGenerator.NewId(),
                accepted.Sequence,
                serviceIds,
                segmentIds,
                now,
                accepted.OriginAirportId,
                accepted.DestinationAirportId,
                accepted.FareBasis,
                accepted.BrandCode,
                accepted.BrandName,
                accepted.FareType,
                accepted.CabinClassId,
                accepted.RbdId,
                accepted.BookingClass,
                accepted.FareOwnerCarrierId,
                accepted.TariffReference,
                accepted.RuleReference,
                accepted.RoutingReference,
                accepted.SourceFareReference,
                accepted.SourceComponentReference), idGenerator);
        }

        private void EnsureFareConstructionIsCoherent(OrderAirFareConstruction construction)
        {
            if (construction.PricingGroups.Count == 0)
                throw ExceptionFactory.FareConstructionRequiresPricingGroup();

            var itemIds = _items.Select(item => item.Id).ToHashSet();
            var travellerIds = _travellers.Select(traveller => traveller.Id).ToHashSet();
            var serviceIds = _orderServices.Select(service => service.Id).ToHashSet();
            var segmentIds = _segments.Select(segment => segment.Id).ToHashSet();

            foreach (var item in construction.Items)
                if (!itemIds.Contains(item.OrderItemId))
                    throw ExceptionFactory.FareConstructionReferenceOutsideOrder("order item", item.OrderItemId);

            foreach (var group in construction.PricingGroups)
            {
                foreach (var traveller in group.Travellers)
                    if (!travellerIds.Contains(traveller.OrderTravellerId))
                        throw ExceptionFactory.FareConstructionReferenceOutsideOrder("traveller", traveller.OrderTravellerId);

                if (group.PricingUnits.Count == 0)
                    throw ExceptionFactory.FarePricingGroupRequiresPricingUnit();

                foreach (var unit in group.PricingUnits)
                {
                    if (unit.FareComponents.Count == 0)
                        throw ExceptionFactory.FarePricingUnitRequiresFareComponent();

                    foreach (var component in unit.FareComponents)
                    {
                        foreach (var service in component.Services)
                            if (!serviceIds.Contains(service.OrderServiceId))
                                throw ExceptionFactory.FareConstructionReferenceOutsideOrder("order service", service.OrderServiceId);

                        foreach (var segment in component.Segments)
                            if (!segmentIds.Contains(segment.OrderSegmentId))
                                throw ExceptionFactory.FareConstructionReferenceOutsideOrder("order segment", segment.OrderSegmentId);
                    }
                }
            }
        }
    }
}
