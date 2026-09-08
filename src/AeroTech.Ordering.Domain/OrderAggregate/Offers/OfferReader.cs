using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Offers
{
    public sealed class OfferReader
    {
        private readonly OfferDetail _offer;

        public OfferReader(OfferDetail offer) => _offer = offer;

        public string SourceOfferId => _offer.OfferId;

        public int CurrencyId => _offer.CurrencyId;

        public DateTimeOffset? LastTicketingDate => _offer.LastTicketingDate;

        public IReadOnlyList<OfferBound> BoundsInSequence()
            => _offer.Bounds.OrderBy(bound => bound.Sequence).ToList();

        public IReadOnlyList<OfferFlight> BoundFlightsInOrder(OfferBound bound)
            => bound.Flights.OrderBy(flight => flight.DepartureDateTime).ToList();

        public string GetTravellerRef(int travellerIndex)
        {
            var traveller = _offer.Travellers.FirstOrDefault(t => t.TravellerIndex == travellerIndex);
            if (traveller is null)
                throw ExceptionFactory.OfferHasNoTravellerWithIndex(travellerIndex);
            return traveller.TravellerRef;
        }

        public OfferFareComponent? PrimaryFareComponent(string boundId)
            => _offer.FareComponents.FirstOrDefault(component => SameRef(component.BoundId, boundId));

        public OfferFareComponent? FareComponent(string boundId, long airFareId)
            => _offer.FareComponents.FirstOrDefault(component => SameRef(component.BoundId, boundId) && component.AirFareId == airFareId)
               ?? PrimaryFareComponent(boundId);

        public IReadOnlyList<OfferPriceLine> BaseLines(string travellerRef, string boundId)
            => _offer.PriceLines
                .Where(line => SameRef(line.TravellerRef, travellerRef) && line.IsBase && SameRef(line.BoundId, boundId))
                .ToList();

        public IReadOnlyList<OfferPriceLine> ChargeLines(string travellerRef)
            => _offer.PriceLines
                .Where(line => SameRef(line.TravellerRef, travellerRef) && !line.IsBase)
                .ToList();

        public IReadOnlyList<OfferPriceLine> OrderChargeLines()
            => _offer.OrderCharges.Where(line => !line.IsBase).ToList();

        public long ResolveTravellerBoundAirFareId(IReadOnlyList<OfferPriceLine> baseLines, string boundId)
        {
            var fromLines = baseLines.Select(line => line.AirFareId ?? 0).FirstOrDefault(id => id > 0);
            if (fromLines > 0)
                return fromLines;

            var fareComponent = PrimaryFareComponent(boundId);
            if (fareComponent is not null && fareComponent.AirFareId > 0)
                return fareComponent.AirFareId;

            throw ExceptionFactory.CouldNotResolveAirFareForBound(boundId);
        }

        public OfferCharge? Charge(string? airChargeId)
            => string.IsNullOrWhiteSpace(airChargeId)
                ? null
                : _offer.Charges.FirstOrDefault(charge => string.Equals(charge.AirChargeId, airChargeId, StringComparison.Ordinal));

        public OfferRate? Rate(string? rateOfExchangePeriodId)
            => string.IsNullOrWhiteSpace(rateOfExchangePeriodId)
                ? null
                : _offer.Rates.FirstOrDefault(rate => string.Equals(rate.RateOfExchangePeriodId, rateOfExchangePeriodId, StringComparison.Ordinal));

        public int SourceCurrencyId(OfferPriceLine line)
        {
            if (line.CurrencyId is > 0)
                return line.CurrencyId.Value;

            var rate = Rate(line.RateOfExchangePeriodId);
            if (rate is not null && rate.FromCurrencyId > 0)
                return rate.FromCurrencyId;

            return _offer.CurrencyId;
        }

        public int EquivalentCurrencyId(OfferPriceLine line)
        {
            var rate = Rate(line.RateOfExchangePeriodId);
            if (rate is not null && rate.ToCurrencyId > 0)
                return rate.ToCurrencyId;

            return _offer.CurrencyId;
        }

        private static bool SameRef(string? left, string? right)
            => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
