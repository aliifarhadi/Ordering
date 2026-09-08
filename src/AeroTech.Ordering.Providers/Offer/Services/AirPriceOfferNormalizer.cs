using AeroTech.Messages.AirPrice.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Providers.Offer.Model;
using BoundDirection = AeroTech.Messages.Ordering.Enums.BoundDirection;
using PassengerTypeCode = AeroTech.Messages.Ordering.Enums.PassengerTypeCode;
using SourcePassengerTypeCode = AeroTech.Messages.AirPrice.Enums.PassengerTypeCode;
using SourceWeightUnit = AeroTech.Messages.AirPrice.Enums.WeightUnit;

namespace AeroTech.Ordering.Providers.Offer.Services
{
    public sealed class AirPriceOfferNormalizer : IAirPriceOfferNormalizer
    {
        public const string SourceSystem = "AirPrice";

        public AcceptedOrderSource Normalize(OfferDetail offer)
        {
            var reader = new OfferReader(offer);
            var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var journeys = NormalizeJourneys(reader);
            var products = new List<AcceptedProduct>();
            var pricingLines = new List<AcceptedSourcePricingLine>();

            foreach (var traveller in offer.Travellers)
                NormalizeTraveller(reader, offer, traveller, products, pricingLines, occurrences);

            NormalizeOrderCharges(reader, offer, pricingLines, occurrences);

            return new AcceptedOrderSource(
                SourceSystem,
                offer.OfferId,
                offer.CurrencyId,
                offer.LastTicketingDate,
                offer.Travellers
                    .Select(candidate => new AcceptedSourceTraveller(candidate.TravellerRef, candidate.TravellerIndex))
                    .ToList(),
                journeys,
                products,
                pricingLines);
        }

        private static IReadOnlyList<AcceptedJourney> NormalizeJourneys(OfferReader reader)
        {
            var bounds = reader.BoundsInSequence();
            var journeys = new List<AcceptedJourney>();

            for (var index = 0; index < bounds.Count; index++)
            {
                var bound = bounds[index];
                var direction = bounds.Count == 1 || index == 0 ? BoundDirection.Outbound : BoundDirection.Inbound;
                var fareComponent = reader.PrimaryFareComponent(bound.BoundId);
                var sequence = 1;

                var segments = reader.BoundFlightsInOrder(bound)
                    .Select(flight => new AcceptedSegment(
                        SegmentRef(bound.BoundId, flight.FlightId),
                        sequence++,
                        flight.FlightId,
                        flight.FlightVersion,
                        flight.Number,
                        (int)flight.OriginAirportId,
                        (int?)flight.OriginAirportTerminalId,
                        (int)flight.DestinationAirportId,
                        (int?)flight.DestinationAirportTerminalId,
                        (int)flight.MarketingAirlineId,
                        (int)flight.OperatingAirlineId,
                        flight.DepartureDateTime,
                        flight.ArrivalDateTime,
                        flight.Duration,
                        (int)(flight.AircraftId ?? 0),
                        (int?)flight.CabinClassId,
                        flight.RbdId,
                        flight.BookingClass,
                        fareComponent?.BookingClass,
                        flight.FlightCapacityId,
                        fareComponent?.AirFareId ?? 0,
                        flight.Legs
                            .OrderBy(leg => leg.Sequence)
                            .Select(leg => new AcceptedSegmentLeg(
                                leg.LegId,
                                leg.Sequence,
                                (int)leg.OriginAirportId,
                                (int?)leg.OriginAirportTerminalId,
                                (int)leg.DestinationAirportId,
                                (int?)leg.DestinationAirportTerminalId,
                                leg.DepartureDateTime,
                                leg.ArrivalDateTime))
                            .ToList()))
                    .ToList();

                journeys.Add(new AcceptedJourney(
                    bound.BoundId,
                    bound.Sequence,
                    bound.OriginAirportId,
                    bound.DestinationAirportId,
                    direction,
                    segments));
            }

            return journeys;
        }

        private static void NormalizeTraveller(
            OfferReader reader,
            OfferDetail offer,
            OfferTraveller traveller,
            List<AcceptedProduct> products,
            List<AcceptedSourcePricingLine> pricingLines,
            Dictionary<string, int> occurrences)
        {
            var productsByKey = new Dictionary<string, AcceptedProductBuilder>(StringComparer.OrdinalIgnoreCase);

            foreach (var bound in reader.BoundsInSequence())
            {
                var baseLines = reader.BaseLines(traveller.TravellerRef, bound.BoundId);
                var airFareId = reader.ResolveTravellerBoundAirFareId(baseLines, bound.BoundId);
                var fareComponent = reader.FareComponent(bound.BoundId, airFareId);
                var fareBasis = fareComponent?.FareBasis ?? string.Empty;
                var productRef = ProductRef(traveller.TravellerRef, airFareId, fareBasis);

                if (!productsByKey.TryGetValue(productRef, out var builder))
                {
                    builder = new AcceptedProductBuilder(
                        productRef,
                        traveller.TravellerRef,
                        airFareId,
                        fareBasis,
                        fareComponent,
                        offer);

                    productsByKey.Add(productRef, builder);
                }

                foreach (var flight in reader.BoundFlightsInOrder(bound))
                    builder.AddService(
                        ServiceRef(traveller.TravellerRef, bound.BoundId, flight.FlightId),
                        traveller.TravellerRef,
                        SegmentRef(bound.BoundId, flight.FlightId),
                        (int)flight.MarketingAirlineId,
                        (int)flight.OperatingAirlineId);

                foreach (var line in baseLines)
                    pricingLines.Add(NormalizePricingLine(
                        reader,
                        offer,
                        line,
                        PricingComponentType.Fare,
                        fareComponent?.FareBasis,
                        null,
                        Refundability(fareComponent?.IsRefundable ?? false),
                        productRef,
                        traveller.TravellerRef,
                        bound.BoundId,
                        occurrences));
            }

            foreach (var line in reader.ChargeLines(traveller.TravellerRef))
            {
                var charge = reader.Charge(line.AirChargeId);
                var owningProduct = productsByKey.Values.FirstOrDefault()?.ProductRef;

                pricingLines.Add(NormalizePricingLine(
                    reader,
                    offer,
                    line,
                    ComponentTypeOf(charge, line.AirChargeId ?? line.Code),
                    charge?.Code ?? line.Code,
                    charge?.Name,
                    Refundability(charge?.IsRefundable ?? false),
                    owningProduct,
                    traveller.TravellerRef,
                    line.BoundId,
                    occurrences));
            }

            products.AddRange(productsByKey.Values.Select(builder => builder.Build()));
        }

        private static void NormalizeOrderCharges(
            OfferReader reader,
            OfferDetail offer,
            List<AcceptedSourcePricingLine> pricingLines,
            Dictionary<string, int> occurrences)
        {
            foreach (var line in reader.OrderChargeLines())
            {
                var charge = reader.Charge(line.AirChargeId);
                var identity = SourceLineIdentity(
                    offer.OfferId,
                    "ORDER",
                    line.BoundId,
                    line.FlightId,
                    line.AirChargeId ?? line.Code);

                pricingLines.Add(new AcceptedSourcePricingLine(
                    ComponentTypeOf(charge, line.AirChargeId ?? line.Code),
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Debit,
                    PricingLineRole.Original,
                    line.Amount,
                    reader.SourceCurrencyId(line),
                    line.EquivalentAmount,
                    reader.EquivalentCurrencyId(line),
                    PricingBasisType.Order,
                    PricingApplicationLevel.PerOrder,
                    Refundability(charge?.IsRefundable ?? false),
                    identity,
                    NextOccurrenceKey(occurrences, identity),
                    null,
                    null,
                    charge?.Code ?? line.Code,
                    charge?.Name,
                    ExchangeRateOf(reader, line)));
            }
        }

        private static AcceptedSourcePricingLine NormalizePricingLine(
            OfferReader reader,
            OfferDetail offer,
            OfferPriceLine line,
            PricingComponentType componentType,
            string? code,
            string? description,
            RefundabilityRule refundability,
            string? productRef,
            string travellerRef,
            string? boundId,
            Dictionary<string, int> occurrences)
        {
            var serviceRef = line.FlightId.HasValue && boundId is not null
                ? ServiceRef(travellerRef, boundId, line.FlightId.Value)
                : null;

            var identity = SourceLineIdentity(
                offer.OfferId,
                travellerRef,
                line.BoundId,
                line.FlightId,
                line.AirChargeId ?? line.AirFareId?.ToString() ?? line.Code);

            return new AcceptedSourcePricingLine(
                componentType,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                line.Amount,
                reader.SourceCurrencyId(line),
                line.EquivalentAmount,
                reader.EquivalentCurrencyId(line),
                serviceRef is null ? PricingBasisType.OrderItem : PricingBasisType.OrderService,
                serviceRef is null ? PricingApplicationLevel.PerTraveler : PricingApplicationLevel.PerSegment,
                refundability,
                identity,
                NextOccurrenceKey(occurrences, identity),
                productRef,
                serviceRef,
                code,
                description,
                ExchangeRateOf(reader, line));
        }

        public static PricingComponentType ComponentTypeOf(OfferCharge? charge, string? sourceReference)
            => charge?.Kind switch
            {
                AirChargeKind.Tax => PricingComponentType.Tax,
                AirChargeKind.Surcharge => PricingComponentType.CarrierSurcharge,
                AirChargeKind.Fee => PricingComponentType.Fee,
                null => throw ExceptionFactory.SourceChargeClassificationUnsupported(sourceReference ?? "(unresolved)"),
                _ => throw ExceptionFactory.SourceChargeClassificationUnsupported(charge.Kind)
            };

        public static CommercialTermState TermStateOf(bool? sourceEvidence)
            => sourceEvidence switch
            {
                true => CommercialTermState.Permitted,
                false => CommercialTermState.Prohibited,
                null => CommercialTermState.Unknown
            };

        public static string? BrandNameOf(string? fareFamily)
            => string.IsNullOrWhiteSpace(fareFamily) ? null : fareFamily.Trim();

        public static AcceptedBaggageAllowance? BaggageOf(int pieces, decimal weight, string? unit)
        {
            if (pieces <= 0 && weight <= 0m)
                return null;

            if (!TryTranslateWeightUnit(unit, out var parsed))
                throw ExceptionFactory.SourceBaggageUnitUnsupported(unit ?? "(none)");

            return new AcceptedBaggageAllowance(pieces, weight, parsed);
        }

        private static bool TryTranslateWeightUnit(string? unit, out BaggageWeightUnit translated)
        {
            if (Enum.TryParse<SourceWeightUnit>(unit, ignoreCase: true, out var sourceUnit))
            {
                translated = sourceUnit switch
                {
                    SourceWeightUnit.Kg => BaggageWeightUnit.Kg,
                    SourceWeightUnit.Lbs => BaggageWeightUnit.Lbs,
                    _ => default
                };

                return translated != default;
            }

            translated = default;

            return false;
        }

        public static PassengerTypeCode TranslatePassengerType(SourcePassengerTypeCode sourceCode)
            => Enum.TryParse<PassengerTypeCode>(sourceCode.ToString(), out var translated)
                ? translated
                : throw ExceptionFactory.SourcePassengerTypeUnsupported(sourceCode);

        private static RefundabilityRule Refundability(bool isRefundable)
            => isRefundable ? RefundabilityRule.Refundable : RefundabilityRule.NonRefundable;

        private static ExchangeRate? ExchangeRateOf(OfferReader reader, OfferPriceLine line)
        {
            var rate = reader.Rate(line.RateOfExchangePeriodId);

            return rate is null
                ? null
                : new ExchangeRate(new ExchangeRateArgs(rate.Rate, rate.DecimalPlaces, rate.RateOfExchangePeriodId, 0));
        }

        internal static string SourceLineIdentity(
            string sourceOfferId,
            string travellerRef,
            string? boundId,
            long? flightId,
            string? code)
            => string.Join(':', sourceOfferId, travellerRef, boundId ?? "-", flightId?.ToString() ?? "-", code ?? "-");

        internal static string NextOccurrenceKey(Dictionary<string, int> occurrences, string sourceLineIdentity)
        {
            var occurrence = occurrences.TryGetValue(sourceLineIdentity, out var previous) ? previous + 1 : 1;
            occurrences[sourceLineIdentity] = occurrence;

            return occurrence.ToString();
        }

        private static string SegmentRef(string boundId, long flightId) => $"{boundId}:{flightId}";

        private static string ServiceRef(string travellerRef, string boundId, long flightId)
            => $"{travellerRef}:{boundId}:{flightId}";

        private static string ProductRef(string travellerRef, long airFareId, string fareBasis)
            => $"{travellerRef}:{airFareId}:{fareBasis}";
    }
}
