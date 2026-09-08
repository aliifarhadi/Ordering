using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderFarePricingGroup : Entity<long>
    {
        private readonly List<OrderFarePricingGroupTraveller> _travellers = new();
        private readonly List<OrderFarePricingUnit> _pricingUnits = new();

        private OrderFarePricingGroup()
        {
        }

        internal OrderFarePricingGroup(long fareConstructionId, CreateOrderFarePricingGroupArgs args, IIdGenerator idGenerator)
        {
            Id = args.Id;
            FareConstructionId = fareConstructionId;
            PassengerType = args.PassengerType;
            SourceReference = args.SourceReference;

            foreach (var travellerId in args.TravellerIds.Distinct())
                _travellers.Add(new OrderFarePricingGroupTraveller(idGenerator.NewId(), Id, travellerId));
        }

        public long FareConstructionId { get; private set; }

        public PassengerTypeCode? PassengerType { get; private set; }

        public string? SourceReference { get; private set; }

        public IReadOnlyCollection<OrderFarePricingGroupTraveller> Travellers => _travellers.AsReadOnly();

        public IReadOnlyCollection<OrderFarePricingUnit> PricingUnits => _pricingUnits.AsReadOnly();

        internal OrderFarePricingUnit AddPricingUnit(CreateOrderFarePricingUnitArgs args, IIdGenerator idGenerator)
        {
            var unit = new OrderFarePricingUnit(Id, args);
            _pricingUnits.Add(unit);

            return unit;
        }
    }
}
