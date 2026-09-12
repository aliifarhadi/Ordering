using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Query.OrderAggregate.View;
using Order = AeroTech.Ordering.Domain.OrderAggregate.Order;

namespace AeroTech.Ordering.Synchronizer.OrderAggregate
{
    public static class OrderViewBuilder
    {
        public static OrderView Build(
            Order order,
            IReadOnlyList<FulfillmentReservation> reservations,
            IReadOnlyList<ElectronicTicket> tickets,
            IReadOnlyList<ElectronicMiscDocument> miscDocuments,
            FulfillmentReservationStatus? reservationSummary,
            long projectionRevision,
            DateTimeOffset updatedAt)
            => new(
                OrderView.CurrentSchemaVersion,
                order.Id,
                order.UniqueIdentifierId,
                order.RecordLocator?.Value,
                order.CommercialVersion,
                order.ObligationVersion,
                projectionRevision,
                updatedAt,
                order.CommercialSummary,
                order.Status,
                order.Type,
                order.Channel,
                order.OwnerAirlineId,
                order.CustomerId,
                order.AirlineOfficeId,
                order.CurrencyId,
                order.Pax,
                order.CreationDate,
                order.TimeToLive,
                BuildTotals(order),
                BuildFacets(order, reservationSummary),
                order.Travellers.OrderBy(traveller => traveller.Index).Select(BuildTraveller).ToList(),
                BuildJourneys(order),
                order.Items.OrderBy(item => item.Id).Select(BuildItem).ToList(),
                order.OrderServices.OrderBy(service => service.Id).Select(service => BuildService(order, service)).ToList(),
                order.FareConstructions.OrderBy(construction => construction.Id).Select(BuildFareConstruction).ToList(),
                BuildChanges(order),
                BuildPricingHistory(order),
                reservations.Select(BuildReservation).ToList(),
                tickets.OrderBy(ticket => ticket.Id).Select(BuildTicket).ToList(),
                miscDocuments.OrderBy(document => document.Id).Select(BuildMiscellaneousDocument).ToList(),
                order.TimeLimits.Select(limit => new OrderViewTimeLimit(limit.Type, limit.DueAt, limit.Status)).ToList(),
                order.ExternalReferences
                    .Select(reference => new OrderViewExternalReference(reference.Type, reference.SourceSystem, reference.Reference))
                    .ToList());

        private static OrderViewTotals BuildTotals(Order order)
            => new(
                order.Amount.GrandTotal,
                order.Amount.BaseFareTotal,
                order.Amount.AncillaryTotal,
                order.Amount.TaxTotal,
                order.Amount.SurchargeTotal,
                order.Amount.FeeTotal,
                order.Amount.DiscountTotal,
                order.Amount.PenaltyTotal,
                order.Commission.CommissionAmount,
                order.Commission.CommissionRate);

        private static OrderViewFacets BuildFacets(Order order, FulfillmentReservationStatus? reservationSummary)
        {
            var requiredTickets = order.RequiredElectronicTicketServiceIds();
            var documentedTickets = order.DocumentedElectronicTicketServiceIds();
            var requiredDocuments = order.RequiredElectronicMiscDocumentServiceIds();
            var documentedDocuments = order.DocumentedElectronicMiscDocumentServiceIds();

            return new OrderViewFacets(
                order.CommercialSummary,
                reservationSummary,
                new OrderViewDocumentFacet(
                    requiredTickets.Count,
                    documentedTickets.Count,
                    requiredTickets.Count > 0 && requiredTickets.All(documentedTickets.Contains)),
                new OrderViewDocumentFacet(
                    requiredDocuments.Count,
                    documentedDocuments.Count,
                    requiredDocuments.Count > 0 && requiredDocuments.All(documentedDocuments.Contains)));
        }

        private static OrderViewTraveller BuildTraveller(OrderTraveller traveller)
            => new(
                traveller.Id,
                traveller.Index,
                traveller.Name.FirstName,
                traveller.Name.SurName,
                traveller.AgeRange,
                traveller.PassengerType);

        private static IReadOnlyList<OrderViewJourney> BuildJourneys(Order order)
            => order.Itineraries
                .OrderBy(itinerary => itinerary.Sequence)
                .Select(itinerary => new OrderViewJourney(
                    itinerary.Id,
                    itinerary.Sequence,
                    itinerary.OriginAirportId,
                    itinerary.DestinationAirportId,
                    itinerary.BoundDirection,
                    order.Segments
                        .Where(segment => segment.OrderItineraryId == itinerary.Id)
                        .OrderBy(segment => segment.Sequence)
                        .Select(segment => new OrderViewSegment(
                            segment.Id,
                            segment.Sequence,
                            segment.Number,
                            segment.MarketingAirlineId,
                            segment.OperatingAirlineId,
                            segment.OriginAirportId,
                            segment.DestinationAirportId,
                            segment.DepartureDateTime,
                            segment.ArrivalDateTime,
                            segment.BookingClass))
                        .ToList()))
                .ToList();

        private static OrderViewItem BuildItem(OrderItem item)
            => new(
                item.Id,
                item.ProductType,
                item.ProductCode,
                item.ProductName,
                item.Quantity,
                item.UnitOfMeasure,
                item.CommercialStatus,
                item.ProductSnapshot is null
                    ? null
                    : new OrderViewProductSnapshot(
                        item.ProductSnapshot.ProductType,
                        item.ProductSnapshot.SourceSystem,
                        item.ProductSnapshot.SourceOfferId,
                        item.ProductSnapshot.SourceProductReference,
                        item.ProductSnapshot.SourcePricingReference,
                        item.ProductSnapshot.ProductCode,
                        item.ProductSnapshot.ProductName,
                        item.ProductSnapshot.BrandCode,
                        item.ProductSnapshot.BrandName,
                        item.ProductSnapshot.MarketingAirlineId,
                        item.ProductSnapshot.OperatingAirlineId,
                        item.ProductSnapshot.SupplierCode,
                        item.ProductSnapshot.AcceptedAt),
                item.CommercialTermsSnapshot is null
                    ? null
                    : new OrderViewCommercialTerms(
                        item.CommercialTermsSnapshot.RefundabilitySummary,
                        item.CommercialTermsSnapshot.ChangeabilitySummary,
                        item.CommercialTermsSnapshot.UpgradeEligibilitySummary,
                        item.CommercialTermsSnapshot.SourceSystem,
                        item.CommercialTermsSnapshot.SourcePolicyReference,
                        item.CommercialTermsSnapshot.SourcePolicyVersion,
                        item.CommercialTermsSnapshot.TermsCapturedAt));

        private static OrderViewService BuildService(Order order, OrderService service)
            => new(
                service.Id,
                service.ServiceType,
                service.ServiceCode,
                service.Name,
                service.Status,
                service.CommercialStatus,
                service.FulfillmentStatus,
                service.DeliveryStatus,
                service.FinancialStatus,
                service.DocumentStatus,
                service.PriceTreatment,
                service.OrderItemId,
                order.ItemServiceLinks
                    .Where(link => link.OrderServiceId == service.Id)
                    .Select(link => link.OrderItemId)
                    .Distinct()
                    .ToList(),
                service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToList(),
                new OrderViewServiceCoverage(
                    service.CoveredServices.Select(covered => covered.CoveredOrderServiceId).ToList(),
                    service.CoveredSegments.Select(covered => covered.OrderSegmentId).ToList()),
                new OrderViewServiceFulfillment(
                    service.DeliveryModel,
                    service.RequiresReservation,
                    service.RequiresSupplierConfirmation,
                    service.RequiresDocument,
                    service.DocumentKind,
                    service.RequiresPaymentCoverage,
                    service.ProviderType,
                    service.SupplierCode,
                    service.HoldBatchId,
                    service.SeatHoldReference),
                BuildServiceDetail(service),
                service.EmdIssuanceSnapshot is null
                    ? null
                    : new OrderViewEmdIssuance(
                        service.EmdIssuanceSnapshot.EmdType,
                        service.EmdIssuanceSnapshot.ReasonForIssuanceCode,
                        service.EmdIssuanceSnapshot.ReasonForIssuanceSubCode,
                        service.EmdIssuanceSnapshot.AssociatedAirOrderServiceId,
                        service.EmdIssuanceSnapshot.SourceSystem,
                        service.EmdIssuanceSnapshot.SourceReference,
                        service.EmdIssuanceSnapshot.CapturedAt),
                service.ElectronicTicketId,
                service.TicketCouponId,
                service.ElectronicMiscDocumentId,
                service.EmdCouponId);

        private static OrderViewServiceDetail BuildServiceDetail(OrderService service)
        {
            if (service.AirTransportDetail is { } air)
                return new OrderViewServiceDetail(
                    "AirTransport",
                    OrderSegmentId: air.OrderSegmentId,
                    TransitionalFareBasis: air.TransitionalFareBasis,
                    RequestedSeat: air.RequestedSeat);

            if (service.SeatDetail is { } seat)
                return new OrderViewServiceDetail(
                    "Seat",
                    AssociatedAirOrderServiceId: seat.AssociatedAirOrderServiceId,
                    SoldSeatNumber: seat.SoldSeatNumber);

            if (service.BaggageDetail is { } baggage)
                return new OrderViewServiceDetail(
                    "Baggage",
                    BaggageKind: baggage.Kind,
                    Pieces: baggage.Pieces,
                    Weight: baggage.Weight,
                    WeightUnit: baggage.WeightUnit,
                    PerPieceWeightLimit: baggage.PerPieceWeightLimit);

            if (service.MealDetail is { } meal)
                return new OrderViewServiceDetail(
                    "Meal",
                    MealCode: meal.MealCode,
                    Quantity: meal.Quantity,
                    SpecialMealCode: meal.SpecialMealCode);

            if (service.LoungeDetail is { } lounge)
                return new OrderViewServiceDetail(
                    "Lounge",
                    AirportId: lounge.AirportId,
                    LoungeCode: lounge.LoungeCode,
                    AccessStart: lounge.AccessStart,
                    AccessEnd: lounge.AccessEnd,
                    GuestCount: lounge.GuestCount,
                    RelatedAirOrderServiceId: lounge.RelatedAirOrderServiceId);

            if (service.HotelDetail is { } hotel)
                return new OrderViewServiceDetail(
                    "Hotel",
                    GuestCount: hotel.GuestCount,
                    PropertyReference: hotel.PropertyReference,
                    CheckIn: hotel.CheckIn,
                    CheckOut: hotel.CheckOut,
                    RoomCount: hotel.RoomCount,
                    RoomTypeCode: hotel.RoomTypeCode);

            if (service.GroundTransportDetail is { } ground)
                return new OrderViewServiceDetail(
                    "GroundTransport",
                    PickupLocationReference: ground.PickupLocationReference,
                    DropoffLocationReference: ground.DropoffLocationReference,
                    PickupAt: ground.PickupAt,
                    PassengerCount: ground.PassengerCount,
                    VehicleTypeCode: ground.VehicleTypeCode);

            if (service.GenericDetail is { } generic)
                return new OrderViewServiceDetail(
                    "Generic",
                    SchemaName: generic.SchemaName,
                    SchemaVersion: generic.SchemaVersion);

            return new OrderViewServiceDetail("None");
        }

        private static OrderViewFareConstruction BuildFareConstruction(OrderAirFareConstruction construction)
            => new(
                construction.Id,
                construction.CreatedByChangeId,
                construction.SupersedesConstructionId,
                construction.ConstructionType,
                construction.SourceSystem,
                construction.SourcePricingReference,
                construction.CreatedAt,
                construction.Items.Select(item => item.OrderItemId).ToList(),
                construction.PricingGroups
                    .OrderBy(group => group.Id)
                    .Select(group => new OrderViewFarePricingGroup(
                        group.Id,
                        group.PassengerType,
                        group.SourceReference,
                        group.Travellers.Select(traveller => traveller.OrderTravellerId).ToList(),
                        group.PricingUnits
                            .OrderBy(unit => unit.Sequence)
                            .Select(unit => new OrderViewFarePricingUnit(
                                unit.Id,
                                unit.PricingUnitType,
                                unit.CombinationMethod,
                                unit.Sequence,
                                unit.SourceReference,
                                unit.FareComponents
                                    .OrderBy(component => component.Sequence)
                                    .Select(BuildFareComponent)
                                    .ToList()))
                            .ToList()))
                    .ToList());

        private static OrderViewFareComponent BuildFareComponent(OrderFareComponent component)
            => new(
                component.Id,
                component.Sequence,
                component.OriginAirportId,
                component.DestinationAirportId,
                component.FareBasis,
                component.BrandCode,
                component.BrandName,
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
                component.Services.Select(service => service.OrderServiceId).ToList(),
                component.Segments.Select(segment => segment.OrderSegmentId).ToList());

        private static IReadOnlyList<OrderViewChange> BuildChanges(Order order)
            => order.Changes
                .OrderBy(change => change.OccurredAt)
                .ThenBy(change => change.Id)
                .Select(change => new OrderViewChange(
                    change.Id,
                    change.ChangeType,
                    change.Source,
                    change.OperationId,
                    change.ExternalReference,
                    change.OccurredAt,
                    order.ItemServiceLinks
                        .Where(link => link.LinkedByChangeId == change.Id)
                        .Select(link => link.OrderItemId)
                        .Distinct()
                        .ToList(),
                    order.ItemServiceLinks
                        .Where(link => link.LinkedByChangeId == change.Id)
                        .Select(link => link.OrderServiceId)
                        .ToList(),
                    order.OriginatingPriceConsequenceOf(change.Id)?.Id))
                .ToList();

        private static IReadOnlyList<OrderViewPriceChangeSet> BuildPricingHistory(Order order)
            => order.PriceChangeSets
                .Where(set => set.IsCommitted)
                .OrderBy(set => set.FinancialSequence)
                .Select(set =>
                {
                    var lines = order.PricingLines.Where(line => line.PriceChangeSetId == set.Id).ToList();

                    return new OrderViewPriceChangeSet(
                        set.Id,
                        set.ChangeId,
                        set.FinancialSequence,
                        set.ExpectedCommercialVersion,
                        set.Reason,
                        set.Source,
                        set.SourceOfferId,
                        set.SourcePricingRef,
                        set.CreatedAt,
                        set.CommittedAt!.Value,
                        lines.Where(line => line.AffectsCustomerBalance).Sum(line => line.SignedSaleAmount),
                        lines.OrderBy(line => line.Id).Select(BuildPricingLine).ToList());
                })
                .ToList();

        private static OrderViewPricingLine BuildPricingLine(OrderPricingLine line)
            => new(
                line.Id,
                line.ComponentType,
                line.Effect,
                line.Direction,
                line.LineRole,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                BuildExchangeRate(line.ExchangeRate),
                line.Refundability,
                line.BasisType,
                line.BasisReferenceId,
                line.OrderItemId,
                line.ApplicationLevel,
                line.Quantity,
                line.UnitOfMeasure,
                line.UnitPrice,
                line.Code,
                line.Description,
                line.SourceLineRef,
                line.OccurrenceKey,
                line.OriginalPricingLineId,
                line.OriginalAllocationId,
                line.RelatedOperationId,
                line.SettlementPartyRef,
                line.SettlementCategory,
                line.AllocationSets
                    .OrderBy(set => set.Id)
                    .Select(set => new OrderViewAllocationSet(
                        set.Id,
                        set.Purpose,
                        set.Version,
                        set.Source,
                        set.Method,
                        set.Completeness,
                        set.SupersedesAllocationSetId,
                        set.PricingContextRef,
                        set.PolicyVersion,
                        set.Allocations
                            .OrderBy(allocation => allocation.Id)
                            .Select(allocation => new OrderViewAllocation(
                                allocation.Id,
                                allocation.SaleAmount,
                                allocation.SaleCurrencyId,
                                allocation.OrderItemIdAtAllocation,
                                allocation.OrderServiceId,
                                allocation.TravellerId,
                                allocation.ItineraryIdAtAllocation,
                                allocation.SegmentIdAtAllocation,
                                allocation.CoveragePortionRef,
                                allocation.OriginalAmount,
                                allocation.OriginalCurrencyId,
                                BuildExchangeRate(allocation.ExchangeRate),
                                allocation.OriginalAllocationId))
                            .ToList()))
                    .ToList());

        private static OrderViewExchangeRate? BuildExchangeRate(Domain.OrderAggregate.ValueObjects.ExchangeRate? rate)
            => rate is null
                ? null
                : new OrderViewExchangeRate(
                    rate.RateOfExchange,
                    rate.NumberOfDecimalPlaces,
                    rate.RateOfExchangeId,
                    rate.RoundingFactor);

        private static OrderViewReservation BuildReservation(FulfillmentReservation reservation)
            => new(
                reservation.Id,
                reservation.Status,
                reservation.ExternalReservationRef,
                reservation.ExpiresAt,
                reservation.Services
                    .Select(service => new OrderViewReservationMember(
                        service.OrderServiceId,
                        service.ObservedStatus,
                        service.ExternalServiceRef))
                    .ToList());

        private static OrderViewElectronicTicket BuildTicket(ElectronicTicket ticket)
            => new(
                ticket.Id,
                ticket.DocumentNumber,
                ticket.TravelerId,
                ticket.StatusSummary,
                ticket.IssuedAt,
                ticket.IssuedTotal,
                ticket.CurrencyId,
                ticket.ProviderReference,
                ticket.DocumentVersion,
                ticket.Coupons
                    .OrderBy(coupon => coupon.CouponNumber)
                    .Select(coupon => new OrderViewTicketCoupon(
                        coupon.Id,
                        coupon.CouponNumber,
                        coupon.CurrentOrderServiceId,
                        coupon.JourneySegmentId,
                        coupon.FinancialStatus,
                        coupon.ControlStatus,
                        coupon.FareBasisSnapshot,
                        coupon.IssuanceValue))
                    .ToList());

        private static OrderViewMiscellaneousDocument BuildMiscellaneousDocument(ElectronicMiscDocument document)
            => new(
                document.Id,
                document.DocumentNumber,
                document.Type,
                document.ReasonForIssuanceCode,
                document.StatusSummary,
                document.TravelerId,
                document.IssuedAt,
                document.IssuedTotal,
                document.CurrencyId,
                document.ProviderReference,
                document.DocumentVersion,
                document.Coupons
                    .OrderBy(coupon => coupon.CouponNumber)
                    .Select(coupon => new OrderViewMiscellaneousCoupon(
                        coupon.Id,
                        coupon.CouponNumber,
                        coupon.Purpose,
                        coupon.ReasonForIssuanceSubCode,
                        coupon.OrderServiceId,
                        coupon.PricingLineId,
                        coupon.AssociatedTicketCouponId,
                        coupon.ExternalValueReference,
                        coupon.IssuanceValue,
                        coupon.Status))
                    .ToList());
    }
}
