using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderFarePricingUnit : Entity<long>
    {
        private readonly List<OrderFareComponent> _fareComponents = new();

        private OrderFarePricingUnit()
        {
        }

        internal OrderFarePricingUnit(long pricingGroupId, CreateOrderFarePricingUnitArgs args)
        {
            Id = args.Id;
            PricingGroupId = pricingGroupId;
            PricingUnitType = args.PricingUnitType;
            CombinationMethod = args.CombinationMethod;
            Sequence = args.Sequence;
            SourceReference = args.SourceReference;
        }

        public long PricingGroupId { get; private set; }

        public FarePricingUnitType? PricingUnitType { get; private set; }

        public FareCombinationMethod? CombinationMethod { get; private set; }

        public int Sequence { get; private set; }

        public string? SourceReference { get; private set; }

        public IReadOnlyCollection<OrderFareComponent> FareComponents => _fareComponents.AsReadOnly();

        internal OrderFareComponent AddFareComponent(CreateOrderFareComponentArgs args, IIdGenerator idGenerator)
        {
            var component = new OrderFareComponent(Id, args, idGenerator);
            _fareComponents.Add(component);

            return component;
        }
    }
}
