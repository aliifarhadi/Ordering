using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderAirTransportService : OrderService
    {
        private OrderAirTransportService()
        {
        }

        public OrderAirTransportService(CreateOrderServiceArgs serviceArgs, CreateOrderAirTransportServiceArgs airArgs)
            : base(serviceArgs)
        {
            OrderSegmentId = airArgs.OrderSegmentId;
            TravellerId = airArgs.TravellerId;
            Seat = airArgs.Seat;
            AirFareId = airArgs.AirFareId;
            FareBasis = airArgs.FareBasis;
            FareFamilyTitle = airArgs.FareFamilyTitle;
            FareNumber = airArgs.FareNumber;
            IsChangeable = airArgs.IsChangeable;
            IsRefundable = airArgs.IsRefundable;
            IsUpgradable = airArgs.IsUpgradable;
            Baggage = airArgs.Baggage;
            CabinBaggage = airArgs.CabinBaggage;
        }

        public long OrderSegmentId { get; private set; }

        public long TravellerId { get; private set; }

        public string? Seat { get; private set; }

        public long? AirFareId { get; private set; }

        public string? FareBasis { get; private set; }

        public string? FareFamilyTitle { get; private set; }

        public long? FareNumber { get; private set; }

        public bool IsChangeable { get; private set; }

        public bool IsRefundable { get; private set; }

        public bool IsUpgradable { get; private set; }

        public Baggage? Baggage { get; private set; }

        public Baggage? CabinBaggage { get; private set; }

        internal OrderAirTransportService CopyTo(long newId, long newOrderId, long newItemId, long newSegmentId, long newTravellerId, string? newHoldBatchId)
        {
            var copy = new OrderAirTransportService(
                new CreateOrderServiceArgs(newId, newOrderId, newItemId, ServiceType, ServiceCode, Name, DeliveryModel,
                    RequiresFulfillment, RequiresSupplierConfirmation, RequiresDocument, ProviderType, SupplierCode, CreatedAt),
                new CreateOrderAirTransportServiceArgs(newSegmentId, newTravellerId, Seat, AirFareId, FareBasis, FareFamilyTitle,
                    FareNumber, IsChangeable, IsRefundable, IsUpgradable, Baggage?.Copy(), CabinBaggage?.Copy()));

            copy.CopyStateFrom(this, newHoldBatchId);
            return copy;
        }
    }
}
