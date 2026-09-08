using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain._Shared.Versioning;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order : AggregateRoot<long>
    {
        public Guid UniqueIdentifierId { get; private set; }
        public RecordLocator? RecordLocator { get; private set; }
        public long OwnerAirlineId { get; private set; }
        public int CommercialVersion { get; private set; }
        public long FinancialSequence { get; private set; }
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
        private readonly List<OrderChange> _changes = new();
        private readonly List<OrderPriceChangeSet> _priceChangeSets = new();
        private readonly List<OrderPricingLine> _pricingLines = new();
        private readonly List<OrderTraveller> _travellers = new();
        private readonly List<OrderSegment> _segments = new();
        private readonly List<OrderService> _orderServices = new();
        private readonly List<OrderItinerary> _itineraries = new();
        private readonly List<OrderAirFareConstruction> _fareConstructions = new();

        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
        public IReadOnlyCollection<OrderChange> Changes => _changes.AsReadOnly();
        public IReadOnlyCollection<OrderPriceChangeSet> PriceChangeSets => _priceChangeSets.AsReadOnly();
        public IReadOnlyCollection<OrderPricingLine> PricingLines => _pricingLines.AsReadOnly();
        public IReadOnlyCollection<OrderTraveller> Travellers => _travellers.AsReadOnly();
        public IReadOnlyCollection<OrderSegment> Segments => _segments.AsReadOnly();
        public IReadOnlyCollection<OrderService> OrderServices => _orderServices.AsReadOnly();
        public IReadOnlyCollection<OrderItinerary> Itineraries => _itineraries.AsReadOnly();
        public IReadOnlyCollection<OrderAirFareConstruction> FareConstructions => _fareConstructions.AsReadOnly();

        private Order()
        {
        }

        private Order(
            long id,
            Guid uniqueIdentifierId,
            long ownerAirlineId,
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
            if (ownerAirlineId <= 0)
                throw ExceptionFactory.OwnerAirlineIdRequired();

            Id = id;
            UniqueIdentifierId = uniqueIdentifierId;
            OwnerAirlineId = ownerAirlineId;
            CustomerId = customerId;
            CreatorUserId = creatorUserId;
            AirlineOfficeId = airlineOfficeId;
            Channel = channel;
            Type = type;
            CurrencyId = currencyId;
            Pax = pax;
            CreationDate = creationDate;
            TimeToLive = timeToLive;
            Status = OrderStatus.Created;
            CommercialSummary = CommercialSummary.Draft;
            Lineage = OrderLineage.Root(id);
            ObligationVersion = 1;
            CommercialVersion = 1;
            FinancialSequence = 0;
            Amount = OrderAmount.Zero();
            Commission = new Commission(0m, 0m);
        }

        private void TransitionTo(OrderStatus status) => Status = status;

        private readonly CommercialEventSequence _eventSequence = new();

        private void IncrementCommercialVersion()
        {
            CommercialVersion++;
            _eventSequence.Reset();
        }

        private int NextEventOrdinal() => _eventSequence.Next();

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
