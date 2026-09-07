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
        public static Order Create(CreateOrderArgs args, OfferDetail offer, IIdGenerator idGenerator, IClock clock)
        {
            var reader = new OfferReader(offer);

            var order = new Order(
                idGenerator.NewId(),
                Guid.NewGuid(),
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
            order.BuildItemsServicesAndPricing(args, reader, idGenerator, clock);
            order.BuildOrderCharges(reader, idGenerator, clock);
            order.AssignInfantParents(args);
            order.RecalculateTotal();
            order.ApplyCommission(args.CommissionRate);
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

        private void BuildItemsServicesAndPricing(CreateOrderArgs args, OfferReader reader, IIdGenerator idGenerator, IClock clock)
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
                    {
                        var refundable = (fareComponent?.IsRefundable ?? false) ? RefundabilityRule.Refundable : RefundabilityRule.NonRefundable;
                        var pricingLine = new OrderPricingLine(new CreateOrderPricingLineArgs(
                            idGenerator.NewId(),
                            Id,
                            OrderPricingReason.InitialSale,
                            OrderPricingLineScope.OrderService,
                            OrderPricingLineCategory.Fare,
                            OrderPricingLineSubCategory.BaseFare,
                            OrderPricingLineDirection.Credit,
                            fareComponent?.FareBasis,
                            null,
                            airFareId.ToString(),
                            line.Amount,
                            reader.SourceCurrencyId(line),
                            false,
                            line.EquivalentAmount,
                            reader.EquivalentCurrencyId(line),
                            BuildExchangeRate(reader, line),
                            refundable));

                        AllocateToCoupon(pricingLine, couponServices, fareItem.Id, line, reader, idGenerator);
                        AddPricingLine(pricingLine);
                    }
                }

                foreach (var line in reader.ChargeLines(travellerRef))
                {
                    var charge = reader.Charge(line.AirChargeId);
                    var isTax = charge?.Kind == AirChargeKind.Tax;
                    var firstItem = itemsByProduct.Values.FirstOrDefault();
                    var refundable = (charge?.IsRefundable ?? false) ? RefundabilityRule.Refundable : RefundabilityRule.NonRefundable;

                    var pricingLine = new OrderPricingLine(new CreateOrderPricingLineArgs(
                        idGenerator.NewId(),
                        Id,
                        OrderPricingReason.InitialSale,
                        line.FlightId.HasValue ? OrderPricingLineScope.OrderService : OrderPricingLineScope.Order,
                        isTax ? OrderPricingLineCategory.Tax : OrderPricingLineCategory.Fee,
                        isTax ? OrderPricingLineSubCategory.Tax : OrderPricingLineSubCategory.ServiceFee,
                        OrderPricingLineDirection.Credit,
                        charge?.Code ?? line.Code,
                        charge?.Name,
                        line.AirChargeId,
                        line.Amount,
                        reader.SourceCurrencyId(line),
                        false,
                        line.EquivalentAmount,
                        reader.EquivalentCurrencyId(line),
                        BuildExchangeRate(reader, line),
                        refundable));

                    AllocateToCoupon(pricingLine, couponServices, firstItem?.Id, line, reader, idGenerator);
                    AddPricingLine(pricingLine);
                }
            }
        }

        private void BuildOrderCharges(OfferReader reader, IIdGenerator idGenerator, IClock clock)
        {
            foreach (var line in reader.OrderChargeLines())
            {
                var charge = reader.Charge(line.AirChargeId);

                var chargeItem = new OrderItem(
                    new CreateOrderItemArgs(idGenerator.NewId(), Id, ProductType.ServiceFee, line.AirChargeId ?? string.Empty, charge?.Name ?? "Service fee", 1m, OrderItemUnitOfMeasure.Each, clock.GetDateTime()),
                    OrderChargePolicy(idGenerator, clock));
                AddItem(chargeItem);

                var pricingLine = new OrderPricingLine(new CreateOrderPricingLineArgs(
                    idGenerator.NewId(),
                    Id,
                    OrderPricingReason.InitialSale,
                    OrderPricingLineScope.OrderItem,
                    OrderPricingLineCategory.Fee,
                    OrderPricingLineSubCategory.ServiceFee,
                    OrderPricingLineDirection.Credit,
                    charge?.Code ?? line.Code,
                    charge?.Name,
                    line.AirChargeId,
                    line.Amount,
                    reader.SourceCurrencyId(line),
                    false,
                    line.EquivalentAmount,
                    reader.EquivalentCurrencyId(line),
                    BuildExchangeRate(reader, line),
                    (charge?.IsRefundable ?? false) ? RefundabilityRule.Refundable : RefundabilityRule.NonRefundable));

                pricingLine.AllocateTo(new CreateOrderPricingLineAllocationArgs(
                    idGenerator.NewId(),
                    chargeItem.Id,
                    null,
                    null,
                    null,
                    line.Amount,
                    reader.SourceCurrencyId(line),
                    line.EquivalentAmount,
                    reader.EquivalentCurrencyId(line),
                    BuildExchangeRate(reader, line)));

                AddPricingLine(pricingLine);
            }
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

        private void RecalculateTotal()
        {
            decimal NetPricingTotal(params OrderPricingLineCategory[] categories)
            {
                var lines = categories.Length == 0
                    ? _pricingLines
                    : _pricingLines.Where(line => categories.Contains(line.LineCategory));

                return lines.Where(line => line.LineDirection == OrderPricingLineDirection.Credit).Sum(line => line.EquivalentAmount)
                     - lines.Where(line => line.LineDirection == OrderPricingLineDirection.Debit).Sum(line => line.EquivalentAmount);
            }

            SetAmount(new OrderAmount(
                NetPricingTotal(OrderPricingLineCategory.Fare),
                NetPricingTotal(OrderPricingLineCategory.Tax),
                NetPricingTotal(OrderPricingLineCategory.Fee),
                NetPricingTotal(OrderPricingLineCategory.CarrierImposedSurcharge),
                NetPricingTotal(OrderPricingLineCategory.Discount),
                NetPricingTotal(OrderPricingLineCategory.Penalty),
                NetPricingTotal()));
        }

        private void ApplyCommission(decimal commissionRate) => SetCommission(new Commission(commissionRate, Amount.GrandTotal));

        private long ItineraryIdFor(string boundId)
            => _itineraries.First(itinerary => string.Equals(itinerary.BoundId, boundId, StringComparison.OrdinalIgnoreCase)).Id;

        private void AllocateToCoupon(
            OrderPricingLine pricingLine,
            IReadOnlyDictionary<long, long> couponServices,
            long? orderItemId,
            OfferPriceLine line,
            OfferReader reader,
            IIdGenerator idGenerator)
        {
            if (!line.FlightId.HasValue || !couponServices.TryGetValue(line.FlightId.Value, out var serviceId))
                return;

            pricingLine.AllocateTo(new CreateOrderPricingLineAllocationArgs(
                idGenerator.NewId(),
                orderItemId,
                serviceId,
                OrderPricingLineAllocationTargetType.OrderAirTransportService,
                serviceId,
                line.Amount,
                reader.SourceCurrencyId(line),
                line.EquivalentAmount,
                reader.EquivalentCurrencyId(line),
                BuildExchangeRate(reader, line)));
        }

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

        private static OrderItemPolicySnapshot OrderChargePolicy(IIdGenerator idGenerator, IClock clock)
            => new(idGenerator.NewId(), 0, new CreateOrderItemPolicySnapshotArgs(
                DeliveryModel.NoFulfillmentRequired,
                AccountingGranularity.Order,
                AssignmentMode.None,
                RequiresPassenger: false,
                RequiresSegment: false,
                RequiresSupplierConfirmation: false,
                RequiresDocument: false,
                RequiresFulfillment: false,
                CanBeUnassignedAtPurchase: true,
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
