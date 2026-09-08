using AeroTech.Ordering.Providers.Offer.Model;
using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Providers.Offer.Services
{
    public static class OfferResponseMapper
    {
        public static OfferDetail ToProviderModel(Wire.FlightOfferDetailResponse source)
        {
            var couponsByTicket = source.Tickets
                .SelectMany(ticket => ticket.Coupons.Select(coupon => (ticket, coupon)))
                .ToList();

            var allCoupons = source.Tickets.SelectMany(ticket => ticket.Coupons).ToList();

            var travellers = source.Tickets
                .Select(ticket => new OfferTraveller(ticket.TravellerRef, ticket.TravellerIndex, ticket.PassengerTypeCode))
                .ToList();

            var bounds = source.AirTransports
                .Select(transport => new OfferBound(
                    transport.BoundId,
                    transport.Sequence,
                    transport.OriginAirportId,
                    transport.DestinationAirportId,
                    transport.Flights.Select(MapFlight).ToList()))
                .ToList();

            var fareComponents = source.PricingUnits
                .SelectMany(unit => unit.FareComponents)
                .SelectMany(fareComponent => allCoupons
                    .Where(coupon => coupon.Pricings.Any(line =>
                        line.Category == Wire.OfferPricingCategory.Fare
                        && line.Reference == fareComponent.AirFareId.ToString()))
                    .Select(coupon => (fareComponent, coupon)))
                .GroupBy(pair => (pair.fareComponent.AirFareId, pair.coupon.BoundId))
                .Select(group =>
                {
                    var (fareComponent, coupon) = group.First();
                    return new OfferFareComponent(
                        fareComponent.AirFareId,
                        coupon.BoundId,
                        fareComponent.BookingClass,
                        fareComponent.FareBasis,
                        fareComponent.FareFamily,
                        coupon.IsRefundable,
                        coupon.IsChangeable,
                        coupon.IsUpgradable,
                        coupon.BaggagePieces,
                        coupon.BaggageWeight,
                        coupon.BaggageUnit,
                        coupon.CabinBaggagePieces,
                        coupon.CabinBaggageWeight,
                        coupon.CabinBaggageUnit);
                })
                .ToList();

            var priceLines = couponsByTicket
                .SelectMany(pair => pair.coupon.Pricings
                    .Select(line => MapPriceLine(pair.ticket.TravellerRef, pair.coupon.BoundId, pair.coupon.FlightId, line)))
                .ToList();

            var orderCharges = source.OrderCharges
                .Select(line => MapPriceLine(string.Empty, null, null, line))
                .ToList();

            var charges = allCoupons.SelectMany(coupon => coupon.Pricings)
                .Concat(source.OrderCharges)
                .Where(line => line.Category != Wire.OfferPricingCategory.Fare && !string.IsNullOrWhiteSpace(line.Reference))
                .GroupBy(line => line.Reference!, StringComparer.Ordinal)
                .Select(group => new OfferCharge(
                    group.Key,
                    KindFrom(group.First().Category),
                    group.First().Code,
                    group.First().Name,
                    false))
                .ToList();

            var rates = source.RatesOfExchange
                .Select(rate => new OfferRate(rate.RateOfExchangePeriodId, rate.FromCurrencyId, rate.ToCurrencyId, rate.Rate, rate.DecimalPlaces))
                .ToList();

            return new OfferDetail(
                source.OfferId,
                source.CurrencyId,
                source.LastTicketingDate,
                travellers,
                bounds,
                fareComponents,
                priceLines,
                orderCharges,
                charges,
                rates);
        }

        private static OfferFlight MapFlight(Wire.OfferFlight flight)
            => new(
                flight.FlightId,
                flight.FlightVersion,
                flight.FlightNumber ?? string.Empty,
                flight.OriginAirportId,
                flight.OriginAirportTerminalId,
                flight.DestinationAirportId,
                flight.DestinationAirportTerminalId,
                flight.OperatingAirlineId,
                flight.MarketingAirlineId,
                flight.DepartureDateTime,
                flight.ArrivalDateTime,
                flight.Duration,
                flight.AircraftId,
                flight.CabinClassId,
                flight.RbdId,
                flight.BookingClass,
                flight.FlightCapacityId,
                flight.Legs
                    .Select(leg => new OfferFlightLeg(
                        leg.LegId,
                        leg.Sequence,
                        leg.OriginAirportId,
                        leg.OriginAirportTerminalId,
                        leg.DestinationAirportId,
                        leg.DestinationAirportTerminalId,
                        leg.DepartureDateTime,
                        leg.ArrivalDateTime,
                        MapStop(leg.Stop)))
                    .ToList());

        private static OfferFlightStop? MapStop(Wire.OfferFlightStop? stop)
            => stop is null
                ? null
                : new OfferFlightStop(
                    stop.DurationMinutes,
                    stop.StopType,
                    stop.PassengersCanBoardOrLeave);

        private static OfferPriceLine MapPriceLine(string travellerRef, string? boundId, long? flightId, Wire.OfferPricingLine line)
        {
            var isBase = line.Category == Wire.OfferPricingCategory.Fare;
            long? airFareId = isBase && long.TryParse(line.Reference, out var parsed) ? parsed : null;
            var airChargeId = isBase ? null : line.Reference;

            return new OfferPriceLine(
                travellerRef,
                isBase,
                airFareId,
                airChargeId,
                line.Code,
                boundId,
                flightId,
                line.Amount,
                line.CurrencyId,
                line.EquivalentAmount,
                line.EquivalentCurrencyId,
                line.RateOfExchangePeriodId);
        }

        private static AirChargeKind KindFrom(Wire.OfferPricingCategory category)
            => category switch
            {
                Wire.OfferPricingCategory.Tax => AirChargeKind.Tax,
                Wire.OfferPricingCategory.Surcharge => AirChargeKind.Surcharge,
                _ => AirChargeKind.Fee
            };
    }
}
