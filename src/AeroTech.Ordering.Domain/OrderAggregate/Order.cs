using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order : AggregateRoot<long>
    {
        public Guid UniqueIdentifierId { get; private set; }
        public RecordLocator? RecordLocator { get; private set; }
        public int OrderVersion { get; private set; }
        public long? LinkedOrderId { get; private set; }
        public string? LinkedPNR { get; private set; }

        //SalesContext

        //SellingAirlineOfficeId or AirlineOfficeId or ResponsibleAirlineOfficeId  // IBE-01 , CallCenter-THR-01 این فروش در ساختار داخلی ایرلاین زیر مسئولیت کدام آفیس است؟ + از configuration آژانس/دفتر/POS resolve و snapshot می‌کنم.
   
        //TravelAgencyId?                  // یعنی طرف فروش ثبت‌شده نزد ایرلاین 
        //TravelAgencyOfficeId?            //THR123 / PCC 
       
        //ActorId?                 // agent/user/system that created the order = USR-78421
        //ActorType                // Customer, AgencyUser, AirlineAgent, ApiClient, System
        
        //PointOfSale CountryCode  // IR
        //SoldAtUtc
        //DistributionChain[]?     // Seller → Distributor(s) → Carrier       

        public SalesChannel Channel { get; private set; }  
        public long AirlineOfficeId { get; private set; }     
        public long CreatorUserId { get; private set; }  
        public long CustomerId { get; private set; }

        public OrderStatus Status { get; private set; }
        public OrderType Type { get; private set; }
        public int Pax { get; private set; } 
        public DateTimeOffset CreationDate { get; private set; }
        public DateTimeOffset? TimeToLive { get; private set; }
        public FulfillmentFailureReason? ReservationFailureReason { get; private set; }
        public Commission Commission { get; private set; } = default!;
        public OrderAmount Amount { get; private set; } = default!;
        public int CurrencyId { get; private set; }
        public OrderContact? Contact { get; private set; }
        
        private readonly List<OrderItem> _items = new();
        private readonly List<OrderPricingLine> _pricingLines = new();
        private readonly List<OrderTraveller> _travellers = new();
        private readonly List<OrderSegment> _segments = new();
        private readonly List<OrderService> _orderServices = new();
        private readonly List<OrderItinerary> _itineraries = new();

        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
        public IReadOnlyCollection<OrderPricingLine> PricingLines => _pricingLines.AsReadOnly();
        public IReadOnlyCollection<OrderTraveller> Travellers => _travellers.AsReadOnly();
        public IReadOnlyCollection<OrderSegment> Segments => _segments.AsReadOnly();
        public IReadOnlyCollection<OrderService> OrderServices => _orderServices.AsReadOnly();
        public IReadOnlyCollection<OrderItinerary> Itineraries => _itineraries.AsReadOnly();

        private Order()
        {
        }

        private Order(
            long id,
            Guid uniqueIdentifierId,
            long customerId,
            long creatorUserId,
            long airlineOfficeId,
            SalesChannel channel,
            OrderType type,
            int currencyId,
            int pax,
            DateTimeOffset creationDate,
            DateTimeOffset? timeToLive)
        {
            Id = id;
            UniqueIdentifierId = uniqueIdentifierId;
            CustomerId = customerId;
            CreatorUserId = creatorUserId;
            AirlineOfficeId = airlineOfficeId;
            Channel = channel;
            Type = type;
            CurrencyId = currencyId;
            Pax = pax;
            CreationDate = creationDate;
            TimeToLive = timeToLive;
            Status = OrderStatus.None;
            OrderVersion = 1;
            Amount = OrderAmount.Zero();
            Commission = new Commission(0m, 0m);
        }

        private void TransitionTo(OrderStatus status)
        {
            OrderStateMachine.EnsureCanTransition(Status, status);
            Status = status;
        }

        private void IncrementVersion() => OrderVersion++;

        private void AddItem(OrderItem item) => _items.Add(item);
        private void AddPricingLine(OrderPricingLine pricingLine) => _pricingLines.Add(pricingLine);
        private void AddTraveller(OrderTraveller traveller) => _travellers.Add(traveller);
        private void AddSegment(OrderSegment segment) => _segments.Add(segment);
        private void AddOrderService(OrderService service) => _orderServices.Add(service);
        private void AddItinerary(OrderItinerary itinerary) => _itineraries.Add(itinerary);
        private void SetContact(OrderContact contact) => Contact = contact;
        private void SetAmount(OrderAmount amount) => Amount = amount;
        private void SetCommission(Commission commission) => Commission = commission;
    }
}
