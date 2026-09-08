using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderAirFareConstruction : Entity<long>
    {
        private readonly List<OrderAirFareConstructionItem> _items = new();
        private readonly List<OrderFarePricingGroup> _pricingGroups = new();

        private OrderAirFareConstruction()
        {
        }

        public OrderAirFareConstruction(CreateOrderAirFareConstructionArgs args)
        {
            if (args.SupersedesConstructionId == args.Id)
                throw ExceptionFactory.FareConstructionCannotSupersedeItself(args.Id);

            Id = args.Id;
            OrderId = args.OrderId;
            CreatedByChangeId = args.CreatedByChangeId;
            SupersedesConstructionId = args.SupersedesConstructionId;
            ConstructionType = args.ConstructionType;
            SourceSystem = args.SourceSystem;
            SourcePricingReference = args.SourcePricingReference;
            CreatedAt = args.CreatedAt;
        }

        public long OrderId { get; private set; }

        public long CreatedByChangeId { get; private set; }

        public long? SupersedesConstructionId { get; private set; }

        public AirFareConstructionType? ConstructionType { get; private set; }

        public string SourceSystem { get; private set; } = default!;

        public string? SourcePricingReference { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public IReadOnlyCollection<OrderAirFareConstructionItem> Items => _items.AsReadOnly();

        public IReadOnlyCollection<OrderFarePricingGroup> PricingGroups => _pricingGroups.AsReadOnly();

        public IEnumerable<OrderFareComponent> FareComponents
            => _pricingGroups.SelectMany(group => group.PricingUnits).SelectMany(unit => unit.FareComponents);

        internal void CoverItem(long orderItemId, IIdGenerator idGenerator)
        {
            if (_items.All(item => item.OrderItemId != orderItemId))
                _items.Add(new OrderAirFareConstructionItem(idGenerator.NewId(), Id, orderItemId));
        }

        internal OrderFarePricingGroup AddPricingGroup(CreateOrderFarePricingGroupArgs args, IIdGenerator idGenerator)
        {
            var group = new OrderFarePricingGroup(Id, args, idGenerator);
            _pricingGroups.Add(group);

            return group;
        }
    }
}
