using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public static Order Create(
            CreateOrderArgs args,
            AcceptedOrderSource source,
            long ownerAirlineId,
            IIdGenerator idGenerator,
            IClock clock)
        {
            EnsureAcceptedSourceIsUsable(args, source);

            var order = new Order(
                idGenerator.NewId(),
                Guid.NewGuid(),
                ownerAirlineId,
                args.CustomerId,
                args.CreatorUserId,
                args.AirlineOfficeId,
                args.Channel,
                OrderType.Normal,
                source.SaleCurrencyId,
                args.Travellers.Count,
                clock.GetDateTime(),
                source.TicketingDeadline);

            order.BuildContact(args, idGenerator);
            order.BuildTravellers(args, idGenerator);
            order.EnsureValidComposition();

            var refs = new AcceptedSourceRefMap();

            order.BuildJourneys(source, refs, idGenerator);
            order.MapTravellerRefs(source, refs);
            order.BuildProductsAndServices(args, source, refs, idGenerator, clock);

            order.AssignInfantParents(args);

            var changeSet = order.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.Create,
                    PriceChangeReason.OriginalSale,
                    PricingSource.OfferProvider,
                    order.AcceptPricingLines(source, refs),
                    SourceOfferId: source.SourceOfferId,
                    ActorId: args.CreatorUserId),
                idGenerator,
                clock);

            order.LinkAcceptedServicesToItems(source, changeSet.ChangeId, refs, idGenerator, clock.GetDateTime());
            order.AcceptFareConstructions(source, changeSet.ChangeId, refs, idGenerator, clock.GetDateTime());
            order.AcceptCommercially();
            order.RaiseCreated(idGenerator, clock);

            order.RaisePricingChanged(
                order._changes.Single(change => change.Id == changeSet.ChangeId),
                changeSet,
                idGenerator,
                clock.GetDateTime());

            return order;
        }

        private static void EnsureAcceptedSourceIsUsable(CreateOrderArgs args, AcceptedOrderSource source)
        {
            if (source.Products.Count == 0)
                throw ExceptionFactory.AcceptedSourceHasNoProducts(source.SourceOfferId);

            if (source.PricingLines.Count == 0)
                throw ExceptionFactory.AcceptedSourceHasNoPricing(source.SourceOfferId);

            foreach (var traveller in args.Travellers)
                if (source.Travellers.All(candidate => candidate.TravellerIndex != traveller.Index))
                    throw ExceptionFactory.AcceptedSourceHasNoTravellerWithIndex(traveller.Index);

            if (source.PricingLines.Any(line => line.Effect == PricingEffect.CustomerBalance
                                                && line.SaleCurrencyId != source.SaleCurrencyId))
                throw ExceptionFactory.AcceptedSourceCurrencyIsInconsistent(source.SourceOfferId);
        }

        private void BuildJourneys(AcceptedOrderSource source, AcceptedSourceRefMap refs, IIdGenerator idGenerator)
        {
            foreach (var journey in source.Journeys.OrderBy(journey => journey.Sequence))
            {
                var itinerary = new OrderItinerary(
                    idGenerator.NewId(),
                    Id,
                    journey.OriginAirportId,
                    journey.DestinationAirportId,
                    journey.JourneyRef,
                    journey.Sequence,
                    journey.Direction);

                AddItinerary(itinerary);

                foreach (var accepted in journey.Segments.OrderBy(segment => segment.Sequence))
                {
                    var segment = new OrderSegment(Id, new CreateOrderSegmentArgs(
                        idGenerator.NewId(),
                        itinerary.Id,
                        accepted.Sequence,
                        accepted.CabinClassId,
                        accepted.RbdId,
                        accepted.BookingClassCode,
                        accepted.CapacityReference,
                        accepted.BookingClass,
                        accepted.FareReference,
                        accepted.FlightSourceId,
                        accepted.FlightVersion,
                        accepted.FlightNumber,
                        accepted.OriginAirportId,
                        accepted.OriginAirportTerminalId,
                        accepted.DestinationAirportId,
                        accepted.DestinationAirportTerminalId,
                        accepted.OperatingAirlineId,
                        accepted.MarketingAirlineId,
                        accepted.DepartureAt,
                        accepted.ArrivalAt,
                        accepted.DurationMinutes,
                        accepted.AircraftId));

                    foreach (var leg in accepted.Legs.OrderBy(leg => leg.Sequence))
                        segment.AddLeg(new CreateOrderSegmentLegArgs(
                            idGenerator.NewId(),
                            leg.Sequence,
                            leg.LegSourceId,
                            leg.OriginAirportId,
                            leg.OriginAirportTerminalId,
                            leg.DestinationAirportId,
                            leg.DestinationAirportTerminalId,
                            leg.DepartureAt,
                            leg.ArrivalAt,
                            null,
                            null));

                    AddSegment(segment);
                    refs.SegmentIds[accepted.SegmentRef] = segment.Id;
                    refs.SegmentJourneyRefs[accepted.SegmentRef] = journey.JourneyRef;
                }
            }
        }

        private void MapTravellerRefs(AcceptedOrderSource source, AcceptedSourceRefMap refs)
        {
            foreach (var accepted in source.Travellers)
            {
                var traveller = _travellers.FirstOrDefault(candidate => candidate.Index == accepted.TravellerIndex);

                if (traveller is null)
                    continue;

                refs.TravellerIds[accepted.TravellerRef] = traveller.Id;
                refs.TravellerIndexes[accepted.TravellerRef] = accepted.TravellerIndex;
            }
        }

        private void BuildProductsAndServices(
            CreateOrderArgs args,
            AcceptedOrderSource source,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator,
            IClock clock)
        {
            var now = clock.GetDateTime();

            foreach (var product in source.Products)
            {
                var itemId = idGenerator.NewId();

                var item = new OrderItem(
                    new CreateOrderItemArgs(
                        itemId,
                        Id,
                        product.ProductType,
                        product.ProductCode,
                        product.ProductName,
                        product.Quantity,
                        product.UnitOfMeasure,
                        now),
                    AirTransportPolicy(idGenerator, clock),
                    ProductSnapshotOf(product.Snapshot, itemId, idGenerator, now),
                    CommercialTermsSnapshotOf(product.CommercialTerms, itemId, idGenerator, now));

                AddItem(item);
                refs.ProductItemIds[product.ProductRef] = item.Id;

                foreach (var accepted in product.Services)
                    BuildService(accepted, item.Id, refs, idGenerator, now);
            }
        }

        private IReadOnlyList<AcceptedPricingLineArgs> AcceptPricingLines(
            AcceptedOrderSource source,
            AcceptedSourceRefMap refs)
        {
            var lines = new List<AcceptedPricingLineArgs>();

            foreach (var line in source.PricingLines)
            {
                long? orderItemId = line.ProductRef is { } productRef && refs.ProductItemIds.TryGetValue(productRef, out var itemId)
                    ? itemId
                    : null;

                long? serviceId = line.ServiceRef is { } serviceRef && refs.ServiceIds.TryGetValue(serviceRef, out var mapped)
                    ? mapped
                    : null;

                if (line.ServiceRef is not null && serviceId is null)
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("service", line.ServiceRef);

                if (line.ProductRef is not null && orderItemId is null)
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("product", line.ProductRef);

                lines.Add(new AcceptedPricingLineArgs(
                    line.ComponentType,
                    line.Effect,
                    line.Direction,
                    line.LineRole,
                    line.OriginalAmount,
                    line.OriginalCurrencyId,
                    line.SaleAmount,
                    line.SaleCurrencyId,
                    line.BasisType,
                    line.Refundability,
                    OrderItemId: orderItemId,
                    Code: line.Code,
                    Description: line.Description,
                    ExchangeRate: line.ExchangeRate,
                    ApplicationLevel: line.ApplicationLevel,
                    BasisReferenceId: BasisReferenceFor(line, orderItemId, serviceId),
                    SourceLineRef: line.SourceLineRef,
                    OccurrenceKey: line.OccurrenceKey));
            }

            return lines;
        }

        private long? BasisReferenceFor(AcceptedSourcePricingLine line, long? orderItemId, long? serviceId)
            => line.BasisType switch
            {
                PricingBasisType.Order => Id,
                PricingBasisType.OrderItem => orderItemId,
                PricingBasisType.OrderService => serviceId,
                _ => null
            };

        private static OrderItemProductSnapshot ProductSnapshotOf(
            AcceptedProductSnapshot snapshot,
            long orderItemId,
            IIdGenerator idGenerator,
            DateTimeOffset acceptedAt)
            => new(idGenerator.NewId(), orderItemId, new CreateOrderItemProductSnapshotArgs(
                snapshot.ProductType,
                snapshot.SourceProductReference,
                snapshot.SourceSystem,
                snapshot.SourceOfferId,
                acceptedAt,
                snapshot.ProductCode,
                snapshot.ProductName,
                snapshot.BrandCode,
                snapshot.BrandName,
                snapshot.MarketingAirlineId,
                snapshot.OperatingAirlineId,
                snapshot.SupplierCode,
                snapshot.SourcePricingReference));

        private static OrderItemCommercialTermsSnapshot CommercialTermsSnapshotOf(
            AcceptedCommercialTerms terms,
            long orderItemId,
            IIdGenerator idGenerator,
            DateTimeOffset capturedAt)
            => new(idGenerator.NewId(), orderItemId, new CreateOrderItemCommercialTermsSnapshotArgs(
                terms.RefundabilitySummary,
                terms.ChangeabilitySummary,
                terms.UpgradeEligibilitySummary,
                terms.SourceSystem,
                capturedAt,
                terms.SourcePolicyReference,
                terms.SourcePolicyVersion));

        private void RaiseCreated(IIdGenerator idGenerator, IClock clock)
            => Causes(new OrderCreated(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                UniqueIdentifierId,
                CustomerId,
                AirlineOfficeId,
                CreatorUserId,
                Channel,
                Status,
                Type,
                CurrencyId,
                Pax,
                Amount.GrandTotal,
                Amount.TaxTotal,
                Commission.CommissionAmount,
                Commission.CommissionRate,
                CommercialVersion,
                NextEventOrdinal(),
                LinkedOrderId,
                LinkedPNR,
                TimeToLive,
                CreationDate));

        private void BuildContact(CreateOrderArgs args, IIdGenerator idGenerator)
        {
            var contact = new OrderContact(idGenerator.NewId(), Id, args.Contact.ContactName);

            foreach (var point in args.Contact.ContactPoints)
                contact.AddContactPoint(new OrderContactPoint(idGenerator.NewId(), contact.Id, point.Type, point.Value, point.CountryCode, point.IsPrimary));

            SetContact(contact);
        }

        private void BuildTravellers(CreateOrderArgs args, IIdGenerator idGenerator)
        {
            foreach (var travellerArgs in args.Travellers.OrderBy(traveller => traveller.Index))
            {
                var documents = travellerArgs.Documents
                    .Select(document => new OrderTravellerDocument(
                        idGenerator.NewId(),
                        document.Type,
                        document.Number,
                        document.ExpiryDate,
                        document.IssuanceCountryId,
                        document.Holder));

                var name = new Name(travellerArgs.FirstName, travellerArgs.SurName, travellerArgs.NoSurname);

                AddTraveller(new OrderTraveller(
                    idGenerator.NewId(),
                    Id,
                    travellerArgs.Index,
                    name,
                    travellerArgs.PassengerType,
                    travellerArgs.AgeRange,
                    travellerArgs.DateOfBirth,
                    travellerArgs.Gender,
                    travellerArgs.NationalityId,
                    travellerArgs.CountryOfResidenceId,
                    documents));
            }
        }

        private void EnsureValidComposition()
        {
            if (!_travellers.Any(traveller => traveller.AgeRange == AgeRange.Adult))
                throw ExceptionFactory.OrderMustIncludeAtLeastOneAdult();

            if (_travellers.Count(traveller => traveller.AgeRange == AgeRange.Infant)
                > _travellers.Count(traveller => traveller.AgeRange == AgeRange.Adult))
                throw ExceptionFactory.OrderCannotHaveMoreInfantsThanAdults();
        }

        private void AssignInfantParents(CreateOrderArgs args)
        {
            foreach (var travellerArgs in args.Travellers.Where(traveller => traveller.ParentIndex.HasValue))
            {
                var infant = _travellers.FirstOrDefault(traveller => traveller.Index == travellerArgs.Index);
                var parent = _travellers.FirstOrDefault(traveller => traveller.Index == travellerArgs.ParentIndex!.Value);

                if (infant is not null && parent is not null)
                    infant.AssignParent(parent.Id);
            }
        }


        private static OrderItemPolicySnapshot AirTransportPolicy(IIdGenerator idGenerator, IClock clock)
            => new(idGenerator.NewId(), 0, new CreateOrderItemPolicySnapshotArgs(
                DeliveryModel.PerPassengerSegment,
                AccountingGranularity.PassengerSegment,
                AssignmentMode.PassengerRequired,
                RequiresPassenger: true,
                RequiresSegment: true,
                RequiresSupplierConfirmation: false,
                RequiresDocument: true,
                RequiresFulfillment: true,
                CanBeUnassignedAtPurchase: false,
                CanBeTransferred: false,
                CanBePartiallyConsumed: false,
                RefundRuleRef: null,
                ChangeRuleRef: null,
                CancellationRuleRef: null,
                SupplierPolicyRef: null,
                clock.GetDateTime(),
                "1"));
    }
}
