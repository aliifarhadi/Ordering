using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class ProductAdditionFactory
    {
        public const string SourceSystem = "AncillaryPricing";
        public const string QuotedOfferId = "QOFFER-1";
        public const string SelectedOfferItemId = "QOFFERITEM-1";
        public const string SourceOfferId = QuotedOfferId;
        public const string ProductRef = "ADD-PRODUCT";
        public const int CurrencyId = MultiPassengerOrderFactory.CurrencyId;
        public const long OperationId = 990_000_001;

        public static AcceptedAddServiceChangeArgs Args(
            AcceptedAddServiceChange accepted,
            long operationId = OperationId)
            => new(accepted, operationId, ActorId: 7, ActorScope: "test|agency");

        public static AcceptedAddServiceChange Addition(
            AcceptedAddedProduct product,
            IReadOnlyList<AcceptedAdditionPricingLine> lines,
            string quotedOfferId = QuotedOfferId,
            string selectedOfferItemId = SelectedOfferItemId)
            => new(
                SourceSystem,
                quotedOfferId,
                selectedOfferItemId,
                PricingSource.PricingEngine,
                product,
                lines,
                SourcePricingReference: null);

        public static AcceptedAddedProduct Product(
            ProductType productType,
            IReadOnlyList<AcceptedAddedService> services,
            decimal quantity = 1m,
            OrderItemUnitOfMeasure unitOfMeasure = OrderItemUnitOfMeasure.Each,
            string productRef = ProductRef,
            string? productCode = null,
            string? productName = null)
            => new(
                productRef,
                productType,
                quantity,
                unitOfMeasure,
                new AcceptedProductSnapshot(
                    productType,
                    $"SRC-{productRef}",
                    SourceSystem,
                    SourceOfferId,
                    ProductCode: productCode,
                    ProductName: productName),
                new AcceptedCommercialTerms(
                    CommercialTermState.Conditional,
                    CommercialTermState.Prohibited,
                    CommercialTermState.Unknown,
                    SourceSystem),
                services,
                productCode,
                productName);

        public static AcceptedAddedService Service(
            string serviceRef,
            OrderServiceType serviceType,
            string serviceCode,
            AcceptedServiceDetail detail,
            IReadOnlyList<long> beneficiaryTravellerIds,
            ServicePriceTreatment priceTreatment = ServicePriceTreatment.SeparatelyPriced,
            bool requiresReservation = false,
            bool requiresDocument = false,
            ServiceDocumentKind? documentKind = null,
            IReadOnlyList<long>? coveredOrderServiceIds = null,
            IReadOnlyList<long>? coveredOrderSegmentIds = null,
            DeliveryModel deliveryModel = DeliveryModel.PerPassengerSegment)
            => new(
                serviceRef,
                serviceType,
                serviceCode,
                serviceCode,
                deliveryModel,
                priceTreatment,
                requiresReservation,
                RequiresSupplierConfirmation: false,
                requiresDocument,
                OrderProviderType.Airline,
                beneficiaryTravellerIds,
                detail,
                documentKind,
                RequiresPaymentCoverage: false,
                SupplierCode: null,
                DeliveryProviderReference: null,
                coveredOrderServiceIds,
                coveredOrderSegmentIds);

        public static AcceptedAdditionPricingLine Line(
            PricingComponentType componentType,
            decimal amount,
            PricingBasisType basisType,
            string? serviceRef = null,
            string? productRef = ProductRef,
            PricingEffect effect = PricingEffect.CustomerBalance,
            OrderPricingLineDirection direction = OrderPricingLineDirection.Debit,
            PricingLineRole lineRole = PricingLineRole.Original,
            string? code = null,
            string? sourceLineRef = null,
            string? occurrenceKey = "1",
            string? settlementPartyRef = null,
            string? settlementCategory = null,
            IReadOnlyList<AcceptedAdditionAllocationSet>? allocationSets = null)
            => new(
                componentType,
                effect,
                direction,
                lineRole,
                amount,
                CurrencyId,
                amount,
                CurrencyId,
                basisType,
                RefundabilityRule.NonRefundable,
                productRef,
                serviceRef,
                code ?? componentType.ToString().ToUpperInvariant(),
                null,
                null,
                basisType == PricingBasisType.OrderService
                    ? PricingApplicationLevel.PerSegment
                    : PricingApplicationLevel.PerOrder,
                null,
                null,
                null,
                sourceLineRef ?? $"{QuotedOfferId}:{componentType}:{serviceRef ?? productRef ?? "-"}",
                occurrenceKey,
                settlementPartyRef,
                settlementCategory,
                allocationSets);

        public static OrderService OutboundAirService(Order order)
            => order.AirTransportServices
                .OrderBy(service => service.SoleBeneficiaryId)
                .ThenBy(service => service.SoldSegmentId)
                .First();

        public static OrderService InboundAirService(Order order)
        {
            var outbound = OutboundAirService(order);

            return order.AirTransportServices
                .First(service => service.SoleBeneficiaryId == outbound.SoleBeneficiaryId
                                  && service.SoldSegmentId != outbound.SoldSegmentId);
        }

        public static AcceptedAddServiceChange Seat(Order order, decimal amount = 200_000m, string? seatNumber = "14C")
        {
            var air = OutboundAirService(order);

            var service = Service(
                "SEAT-1",
                OrderServiceType.SeatAssignment,
                "SEAT",
                new AcceptedAddedSeatDetail(air.Id, seatNumber),
                [air.SoleBeneficiaryId],
                coveredOrderServiceIds: [air.Id]);

            return Addition(
                Product(ProductType.Seat, [service]),
                [Line(PricingComponentType.ProductCharge, amount, PricingBasisType.OrderService, "SEAT-1")]);
        }

        public static AcceptedAddServiceChange SeatWithTaxAndCommission(
            Order order,
            decimal charge = 200_000m,
            decimal tax = 20_000m,
            decimal commission = 10_000m)
        {
            var seat = Seat(order, charge);

            return seat with
            {
                PricingLines =
                [
                    .. seat.PricingLines,
                    Line(PricingComponentType.Tax, tax, PricingBasisType.OrderService, "SEAT-1", code: "I6"),
                    Line(
                        PricingComponentType.Commission,
                        commission,
                        PricingBasisType.OrderItem,
                        effect: PricingEffect.SettlementOnly,
                        code: "COMM",
                        settlementPartyRef: "AGENCY-1",
                        settlementCategory: "Commission")
                ]
            };
        }

        public static AcceptedAddServiceChange RoundTripBaggageBundle(Order order, decimal amount = 500_000m)
        {
            var outbound = OutboundAirService(order);
            var inbound = InboundAirService(order);

            var services = new[]
            {
                Service(
                    "BAG-OUT",
                    OrderServiceType.BaggageAllowance,
                    "BAG",
                    new AcceptedBaggageDetail(BaggageServiceKind.PrepaidPiece, Pieces: 2),
                    [outbound.SoleBeneficiaryId],
                    ServicePriceTreatment.Included,
                    coveredOrderServiceIds: [outbound.Id]),
                Service(
                    "BAG-IN",
                    OrderServiceType.BaggageAllowance,
                    "BAG",
                    new AcceptedBaggageDetail(BaggageServiceKind.PrepaidPiece, Pieces: 2),
                    [inbound.SoleBeneficiaryId],
                    ServicePriceTreatment.Included,
                    coveredOrderServiceIds: [inbound.Id])
            };

            return Addition(
                Product(ProductType.Baggage, services),
                [Line(PricingComponentType.ProductCharge, amount, PricingBasisType.OrderItem)]);
        }

        public static AcceptedAddServiceChange GroundTransport(Order order, decimal amount = 300_000m)
        {
            var travellerIds = order.Travellers.OrderBy(traveller => traveller.Index).Select(traveller => traveller.Id).ToList();

            var service = Service(
                "GRD-1",
                OrderServiceType.GroundTransport,
                "GRD",
                new AcceptedGroundTransportDetail(
                    "LOC-A",
                    "LOC-B",
                    new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero),
                    travellerIds.Count,
                    "VAN"),
                travellerIds,
                deliveryModel: DeliveryModel.PerOrder);

            return Addition(
                Product(ProductType.Transfer, [service]),
                [Line(PricingComponentType.ProductCharge, amount, PricingBasisType.OrderService, "GRD-1")]);
        }

        public static AcceptedAddServiceChange Hotel(Order order, decimal amount = 900_000m, int nights = 3)
        {
            var travellerIds = order.Travellers.OrderBy(traveller => traveller.Index).Select(traveller => traveller.Id).ToList();

            var service = Service(
                "HTL-1",
                OrderServiceType.HotelStay,
                "HTL",
                new AcceptedHotelDetail(
                    "PROP-1",
                    new DateOnly(2026, 10, 1),
                    new DateOnly(2026, 10, 1).AddDays(nights),
                    1,
                    travellerIds.Count,
                    "SUP-1",
                    "DBL",
                    "RATE-1"),
                travellerIds,
                ServicePriceTreatment.Included,
                deliveryModel: DeliveryModel.PerOrder);

            return Addition(
                Product(ProductType.Hotel, [service]),
                [Line(PricingComponentType.ProductCharge, amount, PricingBasisType.OrderItem)]);
        }

        public static AcceptedAddServiceChange WiFi(Order order, decimal amount = 50_000m)
        {
            var air = OutboundAirService(order);

            var service = Service(
                "WIFI-1",
                OrderServiceType.WiFi,
                "WIFI",
                new AcceptedGenericServiceDetail("WiFi", "1.0", """{"accessKind":"FullFlight"}"""),
                [air.SoleBeneficiaryId],
                coveredOrderServiceIds: [air.Id]);

            return Addition(
                Product(ProductType.Ancillary, [service]),
                [Line(PricingComponentType.ProductCharge, amount, PricingBasisType.OrderService, "WIFI-1")]);
        }

        public static AcceptedAddServiceChange ExtraSeat(Order order, decimal amount = 700_000m)
        {
            var air = OutboundAirService(order);

            var service = Service(
                "EXST-1",
                OrderServiceType.ExtraSeat,
                "EXST",
                new AcceptedGenericServiceDetail("ExtraSeat", "1.0", """{"capacityQuantity":1,"reason":"CBBG"}"""),
                [air.SoleBeneficiaryId],
                coveredOrderServiceIds: [air.Id]);

            return Addition(
                Product(ProductType.Ancillary, [service]),
                [Line(PricingComponentType.ProductCharge, amount, PricingBasisType.OrderService, "EXST-1")]);
        }

        public static AcceptedAddServiceChange EmdBaggage(Order order, decimal amount = 400_000m)
        {
            var air = OutboundAirService(order);

            var service = Service(
                "BAG-EMD",
                OrderServiceType.BaggageAllowance,
                "BAG",
                new AcceptedBaggageDetail(BaggageServiceKind.PrepaidPiece, Pieces: 1),
                [air.SoleBeneficiaryId],
                requiresDocument: true,
                documentKind: ServiceDocumentKind.ElectronicMiscDocument,
                coveredOrderServiceIds: [air.Id]);

            return Addition(
                Product(ProductType.Baggage, [service]),
                [Line(PricingComponentType.ProductCharge, amount, PricingBasisType.OrderService, "BAG-EMD")]);
        }

        public static AcceptedAddServiceChange SettlementOnly(Order order, decimal commission = 30_000m)
        {
            var air = OutboundAirService(order);

            var service = Service(
                "MEAL-OPAQUE",
                OrderServiceType.Meal,
                "MEAL",
                new AcceptedMealDetail("VGML", 1),
                [air.SoleBeneficiaryId],
                ServicePriceTreatment.SupplierOpaque,
                coveredOrderServiceIds: [air.Id]);

            return Addition(
                Product(ProductType.Meal, [service]),
                [
                    Line(
                        PricingComponentType.Commission,
                        commission,
                        PricingBasisType.OrderItem,
                        effect: PricingEffect.SettlementOnly,
                        code: "COMM",
                        settlementPartyRef: "AGENCY-1",
                        settlementCategory: "Commission")
                ]);
        }
    }
}
