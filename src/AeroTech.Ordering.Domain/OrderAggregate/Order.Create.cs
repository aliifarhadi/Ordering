using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Offers;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;
using BoundDirection = AeroTech.Messages.Ordering.Enums.BoundDirection;
using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public static Order Create(
            CreateOrderArgs args,
            OfferDetail offer,
            long ownerAirlineId,
            IIdGenerator idGenerator,
            IClock clock)
        {
            var reader = new OfferReader(offer);

            var order = new Order(
                idGenerator.NewId(),
                Guid.NewGuid(),
                ownerAirlineId,
                args.CustomerId,
                args.CreatorUserId,
                args.AirlineOfficeId,
                args.Channel,
                OrderType.Normal,
                reader.CurrencyId,
                args.Travellers.Count,
                clock.GetDateTime(),
                reader.LastTicketingDate);

            order.BuildContact(args, idGenerator);
            order.BuildTravellers(args, idGenerator);
            order.EnsureValidComposition();
            order.BuildItineraries(reader, idGenerator);
            order.BuildSegments(reader, idGenerator);
            var acceptedLines = new List<AcceptedPricingLineArgs>();
            var sourceLineRefs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            order.BuildItemsServicesAndPricing(args, reader, acceptedLines, sourceLineRefs, idGenerator, clock);
            order.BuildOrderCharges(reader, acceptedLines, sourceLineRefs);
            order.AssignInfantParents(args);
            order.SetCommission(new Commission(args.CommissionRate, 0m));

            order.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.Create,
                    PriceChangeReason.OriginalSale,
                    PricingSource.OfferProvider,
                    acceptedLines,
                    SourceOfferId: offer.OfferId,
                    ActorId: args.CreatorUserId),
                idGenerator,
                clock);

            order.AcceptCommercially();
            order.RaiseCreated(idGenerator, clock);

            return order;
        }

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

        private void BuildItineraries(OfferReader reader, IIdGenerator idGenerator)
        {
            var bounds = reader.BoundsInSequence();

            for (var index = 0; index < bounds.Count; index++)
            {
                var bound = bounds[index];
                var direction = bounds.Count == 1 || index == 0 ? BoundDirection.Outbound : BoundDirection.Inbound;

                AddItinerary(new OrderItinerary(
                    idGenerator.NewId(),
                    Id,
                    bound.OriginAirportId,
                    bound.DestinationAirportId,
                    bound.BoundId,
                    bound.Sequence,
                    direction));
            }
        }

        private void BuildSegments(OfferReader reader, IIdGenerator idGenerator)
        {
            foreach (var bound in reader.BoundsInSequence())
            {
                var itineraryId = ItineraryIdFor(bound.BoundId);
                var fareComponent = reader.PrimaryFareComponent(bound.BoundId);
                var sequence = 1;

                foreach (var flight in reader.BoundFlightsInOrder(bound))
                {
                    var segment = new OrderSegment(Id, new CreateOrderSegmentArgs(
                        idGenerator.NewId(),
                        itineraryId,
                        sequence++,
                        (int?)flight.CabinClassId,
                        flight.RbdId,
                        fareComponent?.BookingClass,
                        flight.FlightCapacityId,
                        flight.BookingClass,
                        fareComponent?.AirFareId ?? 0,
                        flight.FlightId,
                        flight.FlightVersion,
                        flight.Number,
                        (int)flight.OriginAirportId,
                        (int?)flight.OriginAirportTerminalId,
                        (int)flight.DestinationAirportId,
                        (int?)flight.DestinationAirportTerminalId,
                        (int)flight.OperatingAirlineId,
                        (int)flight.MarketingAirlineId,
                        flight.DepartureDateTime,
                        flight.ArrivalDateTime,
                        flight.Duration,
                        (int)(flight.AircraftId ?? 0)));

                    foreach (var leg in flight.Legs.OrderBy(item => item.Sequence))
                    {
                        segment.AddLeg(new CreateOrderSegmentLegArgs(
                            idGenerator.NewId(),
                            leg.Sequence,
                            leg.LegId,
                            (int)leg.OriginAirportId,
                            (int?)leg.OriginAirportTerminalId,
                            (int)leg.DestinationAirportId,
                            (int?)leg.DestinationAirportTerminalId,
                            leg.DepartureDateTime,
                            leg.ArrivalDateTime,
                            null,
                            null));
                    }

                    AddSegment(segment);
                }
            }
        }

        private void BuildItemsServicesAndPricing(
            CreateOrderArgs args,
            OfferReader reader,
            List<AcceptedPricingLineArgs> acceptedLines,
            Dictionary<string, int> sourceLineRefs,
            IIdGenerator idGenerator,
            IClock clock)
        {
            foreach (var traveller in _travellers)
            {
                var travellerRef = reader.GetTravellerRef(traveller.Index);
                var itemsByProduct = new Dictionary<(ProductType, string, string), OrderItem>();
                var couponServices = new Dictionary<long, long>();

                foreach (var bound in reader.BoundsInSequence())
                {
                    var baseLines = reader.BaseLines(travellerRef, bound.BoundId);
                    var airFareId = reader.ResolveTravellerBoundAirFareId(baseLines, bound.BoundId);
                    var fareComponent = reader.FareComponent(bound.BoundId, airFareId);
                    var fareBasis = fareComponent?.FareBasis ?? string.Empty;
                    var productKey = (ProductType.AirFare, airFareId.ToString(), fareBasis);

                    if (!itemsByProduct.TryGetValue(productKey, out var fareItem))
                    {
                        fareItem = new OrderItem(
                            new CreateOrderItemArgs(idGenerator.NewId(), Id, ProductType.AirFare, airFareId.ToString(), fareBasis, 1m, OrderItemUnitOfMeasure.PassengerFare, clock.GetDateTime()),
                            AirTransportPolicy(idGenerator, clock));
                        AddItem(fareItem);
                        itemsByProduct.Add(productKey, fareItem);
                    }

                    foreach (var flight in reader.BoundFlightsInOrder(bound))
                    {
                        var segment = _segments.Single(candidate => candidate.FlightId == flight.FlightId);

                        var service = new OrderAirTransportService(
                            new CreateOrderServiceArgs(
                                idGenerator.NewId(),
                                Id,
                                fareItem.Id,
                                OrderServiceType.AirTransportation,
                                "AIR",
                                "Air transportation",
                                DeliveryModel.PerPassengerSegment,
                                true,
                                false,
                                true,
                                OrderProviderType.Airline,
                                null,
                                clock.GetDateTime()),
                            new CreateOrderAirTransportServiceArgs(
                                segment.Id,
                                traveller.Id,
                                ResolveSeat(args, bound.BoundId, traveller.Index),
                                airFareId,
                                fareComponent?.FareBasis,
                                fareComponent?.FareFamily,
                                null,
                                fareComponent?.IsChangeable ?? false,
                                fareComponent?.IsRefundable ?? false,
                                fareComponent?.IsUpgradable ?? false,
                                ResolveCheckedBaggage(fareComponent),
                                ResolveCabinBaggage(fareComponent)));

                        AddOrderService(service);
                        couponServices[flight.FlightId] = service.Id;
                    }

                    foreach (var line in baseLines)
                        acceptedLines.Add(AcceptedLine(
                            reader,
                            line,
                            PricingComponentType.Fare,
                            fareComponent?.FareBasis,
                            null,
                            (fareComponent?.IsRefundable ?? false) ? RefundabilityRule.Refundable : RefundabilityRule.NonRefundable,
                            fareItem.Id,
                            couponServices,
                            sourceLineRefs,
                            reader.SourceOfferId,
                            travellerRef));
                }

                foreach (var line in reader.ChargeLines(travellerRef))
                {
                    var charge = reader.Charge(line.AirChargeId);
                    var firstItem = itemsByProduct.Values.FirstOrDefault();

                    acceptedLines.Add(AcceptedLine(
                        reader,
                        line,
                        ComponentTypeOf(charge?.Kind),
                        charge?.Code ?? line.Code,
                        charge?.Name,
                        (charge?.IsRefundable ?? false) ? RefundabilityRule.Refundable : RefundabilityRule.NonRefundable,
                        firstItem?.Id,
                        couponServices,
                        sourceLineRefs,
                        reader.SourceOfferId,
                        travellerRef));
                }
            }
        }

        private void BuildOrderCharges(
            OfferReader reader,
            List<AcceptedPricingLineArgs> acceptedLines,
            Dictionary<string, int> sourceLineRefs)
        {
            foreach (var line in reader.OrderChargeLines())
            {
                var charge = reader.Charge(line.AirChargeId);

                acceptedLines.Add(new AcceptedPricingLineArgs(
                    ComponentTypeOf(charge?.Kind),
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Debit,
                    PricingLineRole.Original,
                    line.Amount,
                    reader.SourceCurrencyId(line),
                    line.EquivalentAmount,
                    reader.EquivalentCurrencyId(line),
                    PricingBasisType.Order,
                    (charge?.IsRefundable ?? false) ? RefundabilityRule.Refundable : RefundabilityRule.NonRefundable,
                    Code: charge?.Code ?? line.Code,
                    Description: charge?.Name,
                    ExchangeRate: BuildExchangeRate(reader, line),
                    ApplicationLevel: PricingApplicationLevel.PerOrder,
                    BasisReferenceId: Id,
                    SourceLineRef: NextSourceLineRef(
                        sourceLineRefs,
                        reader.SourceOfferId,
                        "ORDER",
                        line.BoundId,
                        line.FlightId,
                        line.AirChargeId ?? line.Code)));
            }
        }

        private AcceptedPricingLineArgs AcceptedLine(
            OfferReader reader,
            OfferPriceLine line,
            PricingComponentType componentType,
            string? code,
            string? description,
            RefundabilityRule refundability,
            long? orderItemId,
            IReadOnlyDictionary<long, long> couponServices,
            Dictionary<string, int> sourceLineRefs,
            string sourceOfferId,
            string travellerRef)
        {
            var hasService = line.FlightId.HasValue && couponServices.TryGetValue(line.FlightId.Value, out var serviceId);

            return new AcceptedPricingLineArgs(
                componentType,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                line.Amount,
                reader.SourceCurrencyId(line),
                line.EquivalentAmount,
                reader.EquivalentCurrencyId(line),
                hasService ? PricingBasisType.OrderService : PricingBasisType.OrderItem,
                refundability,
                OrderItemId: orderItemId,
                Code: code,
                Description: description,
                ExchangeRate: BuildExchangeRate(reader, line),
                ApplicationLevel: hasService ? PricingApplicationLevel.PerSegment : PricingApplicationLevel.PerTraveler,
                BasisReferenceId: hasService ? couponServices[line.FlightId!.Value] : orderItemId,
                SourceLineRef: NextSourceLineRef(
                    sourceLineRefs,
                    sourceOfferId,
                    travellerRef,
                    line.BoundId,
                    line.FlightId,
                    line.AirChargeId ?? line.AirFareId?.ToString() ?? line.Code));
        }

        private static PricingComponentType ComponentTypeOf(AirChargeKind? kind)
            => kind switch
            {
                AirChargeKind.Tax => PricingComponentType.Tax,
                AirChargeKind.Surcharge => PricingComponentType.CarrierSurcharge,
                _ => PricingComponentType.Fee
            };

        private static string NextSourceLineRef(
            Dictionary<string, int> sourceLineRefs,
            string sourceOfferId,
            string travellerRef,
            string? boundId,
            long? flightId,
            string? code)
        {
            var key = string.Join(':', sourceOfferId, travellerRef, boundId ?? "-", flightId?.ToString() ?? "-", code ?? "-");
            var occurrence = sourceLineRefs.TryGetValue(key, out var previous) ? previous + 1 : 1;
            sourceLineRefs[key] = occurrence;

            return $"{key}:{occurrence}";
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

        private long ItineraryIdFor(string boundId)
            => _itineraries.First(itinerary => string.Equals(itinerary.BoundId, boundId, StringComparison.OrdinalIgnoreCase)).Id;

        private static ExchangeRate? BuildExchangeRate(OfferReader reader, OfferPriceLine line)
        {
            var rate = reader.Rate(line.RateOfExchangePeriodId);
            return rate is null
                ? null
                : new ExchangeRate(new ExchangeRateArgs(rate.Rate, rate.DecimalPlaces, rate.RateOfExchangePeriodId, 0));
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

        private static string? ResolveSeat(CreateOrderArgs args, string boundId, int travellerIndex)
            => args.SeatSelections
                .FirstOrDefault(selection => string.Equals(selection.BoundId, boundId, StringComparison.OrdinalIgnoreCase)
                                             && selection.TravellerIndex == travellerIndex)
                ?.SeatNumber;

        private static Baggage? ResolveCheckedBaggage(OfferFareComponent? fareComponent)
            => fareComponent is null || (fareComponent.BaggagePieces <= 0 && fareComponent.BaggageWeight <= 0)
                ? null
                : new Baggage(fareComponent.BaggageWeight, ParseWeightUnit(fareComponent.BaggageUnit), fareComponent.BaggagePieces);

        private static Baggage? ResolveCabinBaggage(OfferFareComponent? fareComponent)
            => fareComponent is null || (fareComponent.CabinBaggagePieces <= 0 && fareComponent.CabinBaggageWeight <= 0)
                ? null
                : new Baggage(fareComponent.CabinBaggageWeight, ParseWeightUnit(fareComponent.CabinBaggageUnit), fareComponent.CabinBaggagePieces);

        private static WeightUnit ParseWeightUnit(string? unit)
            => Enum.TryParse<WeightUnit>(unit, ignoreCase: true, out var parsed) ? parsed : WeightUnit.Kg;
    }
}
