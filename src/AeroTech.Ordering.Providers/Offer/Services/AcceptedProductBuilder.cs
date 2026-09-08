using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Providers.Offer.Model;

namespace AeroTech.Ordering.Providers.Offer.Services
{
    internal sealed class AcceptedProductBuilder
    {
        private const string AirServiceCode = "AIR";
        private const string AirServiceName = "Air transportation";

        private readonly List<AcceptedService> _services = new();
        private readonly HashSet<int> _marketingAirlineIds = new();
        private readonly HashSet<int> _operatingAirlineIds = new();
        private readonly long _airFareId;
        private readonly string _fareBasis;
        private readonly OfferFareComponent? _fareComponent;
        private readonly OfferDetail _offer;

        public AcceptedProductBuilder(
            string productRef,
            string travellerRef,
            long airFareId,
            string fareBasis,
            OfferFareComponent? fareComponent,
            OfferDetail offer)
        {
            ProductRef = productRef;
            TravellerRef = travellerRef;
            _airFareId = airFareId;
            _fareBasis = fareBasis;
            _fareComponent = fareComponent;
            _offer = offer;
        }

        public string ProductRef { get; }

        public string TravellerRef { get; }

        public void AddService(
            string serviceRef,
            string travellerRef,
            string segmentRef,
            int marketingAirlineId,
            int operatingAirlineId)
        {
            _marketingAirlineIds.Add(marketingAirlineId);
            _operatingAirlineIds.Add(operatingAirlineId);

            _services.Add(new AcceptedService(
                serviceRef,
                travellerRef,
                segmentRef,
                OrderServiceType.AirTransportation,
                AirServiceCode,
                AirServiceName,
                DeliveryModel.PerPassengerSegment,
                RequiresFulfillment: true,
                RequiresSupplierConfirmation: false,
                RequiresDocument: true,
                OrderProviderType.Airline,
                SupplierCode: null,
                new AcceptedAirServiceDetail(
                    _airFareId,
                    _fareComponent?.FareBasis,
                    _fareComponent?.FareFamily,
                    CheckedBaggage(),
                    CabinBaggage())));
        }

        public AcceptedProduct Build()
            => new(
                ProductRef,
                TravellerRef,
                ProductType.AirFare,
                1m,
                OrderItemUnitOfMeasure.PassengerFare,
                new AcceptedProductSnapshot(
                    ProductType.AirFare,
                    _airFareId.ToString(),
                    AirPriceOfferNormalizer.SourceSystem,
                    _offer.OfferId,
                    ProductCode: null,
                    ProductName: null,
                    BrandCode: null,
                    BrandName: AirPriceOfferNormalizer.BrandNameOf(_fareComponent?.FareFamily),
                    MarketingAirlineId: Unambiguous(_marketingAirlineIds),
                    OperatingAirlineId: Unambiguous(_operatingAirlineIds),
                    SupplierCode: null,
                    SourcePricingReference: _airFareId.ToString()),
                new AcceptedCommercialTerms(
                    AirPriceOfferNormalizer.TermStateOf(_fareComponent?.IsRefundable),
                    AirPriceOfferNormalizer.TermStateOf(_fareComponent?.IsChangeable),
                    AirPriceOfferNormalizer.TermStateOf(_fareComponent?.IsUpgradable),
                    AirPriceOfferNormalizer.SourceSystem,
                    SourcePolicyReference: _airFareId.ToString()),
                _services);

        private AcceptedBaggageAllowance? CheckedBaggage()
            => _fareComponent is null
                ? null
                : AirPriceOfferNormalizer.BaggageOf(
                    _fareComponent.BaggagePieces,
                    _fareComponent.BaggageWeight,
                    _fareComponent.BaggageUnit);

        private AcceptedBaggageAllowance? CabinBaggage()
            => _fareComponent is null
                ? null
                : AirPriceOfferNormalizer.BaggageOf(
                    _fareComponent.CabinBaggagePieces,
                    _fareComponent.CabinBaggageWeight,
                    _fareComponent.CabinBaggageUnit);

        private static int? Unambiguous(HashSet<int> values)
            => values.Count == 1 ? values.Single() : null;
    }
}
