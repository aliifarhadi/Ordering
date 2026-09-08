using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderFareComponent : Entity<long>
    {
        private readonly List<OrderFareComponentService> _services = new();
        private readonly List<OrderFareComponentSegment> _segments = new();

        private OrderFareComponent()
        {
        }

        internal OrderFareComponent(long pricingUnitId, CreateOrderFareComponentArgs args, IIdGenerator idGenerator)
        {
            Id = args.Id;
            PricingUnitId = pricingUnitId;
            Sequence = args.Sequence;
            OriginAirportId = args.OriginAirportId;
            DestinationAirportId = args.DestinationAirportId;
            FareBasis = args.FareBasis;
            BrandCode = args.BrandCode;
            BrandName = args.BrandName;
            FareType = args.FareType;
            CabinClassId = args.CabinClassId;
            RbdId = args.RbdId;
            BookingClass = args.BookingClass;
            FareOwnerCarrierId = args.FareOwnerCarrierId;
            TariffReference = args.TariffReference;
            RuleReference = args.RuleReference;
            RoutingReference = args.RoutingReference;
            SourceFareReference = args.SourceFareReference;
            SourceComponentReference = args.SourceComponentReference;
            CreatedAt = args.CreatedAt;

            foreach (var serviceId in args.OrderServiceIds.Distinct())
                _services.Add(new OrderFareComponentService(idGenerator.NewId(), Id, serviceId));

            foreach (var segmentId in args.OrderSegmentIds.Distinct())
                _segments.Add(new OrderFareComponentSegment(idGenerator.NewId(), Id, segmentId));
        }

        public long PricingUnitId { get; private set; }

        public int Sequence { get; private set; }

        public int? OriginAirportId { get; private set; }

        public int? DestinationAirportId { get; private set; }

        public string? FareBasis { get; private set; }

        public string? BrandCode { get; private set; }

        public string? BrandName { get; private set; }

        public string? FareType { get; private set; }

        public int? CabinClassId { get; private set; }

        public long? RbdId { get; private set; }

        public string? BookingClass { get; private set; }

        public int? FareOwnerCarrierId { get; private set; }

        public string? TariffReference { get; private set; }

        public string? RuleReference { get; private set; }

        public string? RoutingReference { get; private set; }

        public string? SourceFareReference { get; private set; }

        public string? SourceComponentReference { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public IReadOnlyCollection<OrderFareComponentService> Services => _services.AsReadOnly();

        public IReadOnlyCollection<OrderFareComponentSegment> Segments => _segments.AsReadOnly();

        public bool Covers(long orderServiceId) => _services.Any(service => service.OrderServiceId == orderServiceId);
    }
}
