using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderService : Entity<long>
    {
        private readonly List<OrderServiceBeneficiary> _beneficiaries = new();
        private readonly List<OrderServiceCoveredService> _coveredServices = new();
        private readonly List<OrderServiceCoveredSegment> _coveredSegments = new();

        private OrderService()
        {
        }

        public OrderService(CreateOrderServiceArgs args)
        {
            Id = args.Id;
            OrderId = args.OrderId;
            OrderItemId = args.OrderItemId;
            ServiceType = args.ServiceType;
            ServiceCode = args.ServiceCode;
            Name = args.Name;
            Status = OrderServiceStatus.Pending;
            CommercialStatus = OrderServiceCommercialStatus.Pending;
            FulfillmentStatus = OrderFulfillmentStatus.Pending;
            DeliveryStatus = OrderServiceDeliveryStatus.NotReady;
            FinancialStatus = OrderServiceFinancialStatus.Priced;
            DocumentStatus = OrderServiceDocumentStatus.Pending;
            DeliveryModel = args.DeliveryModel;
            PriceTreatment = args.PriceTreatment;
            RequiresReservation = args.RequiresReservation;
            RequiresSupplierConfirmation = args.RequiresSupplierConfirmation;
            RequiresDocument = args.RequiresDocument;
            DocumentKind = args.DocumentKind;
            RequiresPaymentCoverage = args.RequiresPaymentCoverage;
            ProviderType = args.ProviderType;
            SupplierCode = args.SupplierCode;
            DeliveryProviderReference = args.DeliveryProviderReference;
            CreatedAt = args.CreatedAt;
        }

        public long OrderId { get; private set; }

        public long OrderItemId { get; private set; }

        public OrderServiceType ServiceType { get; private set; }

        public string ServiceCode { get; private set; } = default!;

        public string Name { get; private set; } = default!;

        public OrderServiceStatus Status { get; private set; }

        public OrderServiceCommercialStatus CommercialStatus { get; private set; }

        public OrderFulfillmentStatus FulfillmentStatus { get; private set; }

        public OrderServiceDeliveryStatus DeliveryStatus { get; private set; }

        public OrderServiceFinancialStatus FinancialStatus { get; private set; }

        public OrderServiceDocumentStatus DocumentStatus { get; private set; }

        public DeliveryModel DeliveryModel { get; private set; }

        public ServicePriceTreatment PriceTreatment { get; private set; }

        public bool RequiresReservation { get; private set; }

        public bool RequiresSupplierConfirmation { get; private set; }

        public bool RequiresDocument { get; private set; }

        public ServiceDocumentKind? DocumentKind { get; private set; }

        public bool RequiresPaymentCoverage { get; private set; }

        public OrderProviderType ProviderType { get; private set; }

        public string? SupplierCode { get; private set; }

        public string? DeliveryProviderReference { get; private set; }

        public string? HoldBatchId { get; private set; }

        public string? SeatHoldReference { get; private set; }

        public long? TrafficDocumentId { get; private set; }

        public long? DocumentCouponId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public long? ElectronicTicketId { get; private set; }

        public long? TicketCouponId { get; private set; }

        public long? ElectronicMiscDocumentId { get; private set; }

        public long? EmdCouponId { get; private set; }

        public OrderServiceEmdIssuanceSnapshot? EmdIssuanceSnapshot { get; private set; }

        public IReadOnlyCollection<OrderServiceBeneficiary> Beneficiaries => _beneficiaries.AsReadOnly();

        public IReadOnlyCollection<OrderServiceCoveredService> CoveredServices => _coveredServices.AsReadOnly();

        public IReadOnlyCollection<OrderServiceCoveredSegment> CoveredSegments => _coveredSegments.AsReadOnly();

        public OrderAirTransportServiceDetail? AirTransportDetail { get; private set; }

        public OrderSeatServiceDetail? SeatDetail { get; private set; }

        public OrderBaggageServiceDetail? BaggageDetail { get; private set; }

        public OrderMealServiceDetail? MealDetail { get; private set; }

        public OrderLoungeServiceDetail? LoungeDetail { get; private set; }

        public OrderHotelServiceDetail? HotelDetail { get; private set; }

        public OrderGroundTransportServiceDetail? GroundTransportDetail { get; private set; }

        public OrderGenericServiceDetail? GenericDetail { get; private set; }

        public bool IsAirTransport => ServiceType == OrderServiceType.AirTransportation;

        public long SoleBeneficiaryId => _beneficiaries.Count == 1
            ? _beneficiaries[0].OrderTravellerId
            : throw ExceptionFactory.ServiceRequiresExactlyOneBeneficiary(Id, _beneficiaries.Count);

        public long? SoldSegmentId => AirTransportDetail?.OrderSegmentId;

        public bool CoversTraveller(long orderTravellerId)
            => _beneficiaries.Any(beneficiary => beneficiary.OrderTravellerId == orderTravellerId);

        internal void AddBeneficiary(long id, long orderTravellerId)
        {
            if (_beneficiaries.All(beneficiary => beneficiary.OrderTravellerId != orderTravellerId))
                _beneficiaries.Add(new OrderServiceBeneficiary(id, Id, orderTravellerId));
        }

        internal void CoverService(long id, long coveredOrderServiceId)
        {
            if (_coveredServices.All(covered => covered.CoveredOrderServiceId != coveredOrderServiceId))
                _coveredServices.Add(new OrderServiceCoveredService(id, Id, coveredOrderServiceId));
        }

        internal void CoverSegment(long id, long orderSegmentId)
        {
            if (_coveredSegments.All(covered => covered.OrderSegmentId != orderSegmentId))
                _coveredSegments.Add(new OrderServiceCoveredSegment(id, Id, orderSegmentId));
        }

        internal void AttachAirTransport(OrderAirTransportServiceDetail detail)
        {
            EnsureNoDetailAttached();
            AirTransportDetail = detail;
        }

        internal void AttachSeat(OrderSeatServiceDetail detail)
        {
            EnsureNoDetailAttached();
            SeatDetail = detail;
        }

        internal void AttachBaggage(OrderBaggageServiceDetail detail)
        {
            EnsureNoDetailAttached();
            BaggageDetail = detail;
        }

        internal void AttachMeal(OrderMealServiceDetail detail)
        {
            EnsureNoDetailAttached();
            MealDetail = detail;
        }

        internal void AttachLounge(OrderLoungeServiceDetail detail)
        {
            EnsureNoDetailAttached();
            LoungeDetail = detail;
        }

        internal void AttachHotel(OrderHotelServiceDetail detail)
        {
            EnsureNoDetailAttached();
            HotelDetail = detail;
        }

        internal void AttachGroundTransport(OrderGroundTransportServiceDetail detail)
        {
            EnsureNoDetailAttached();
            GroundTransportDetail = detail;
        }

        internal void AttachGeneric(OrderGenericServiceDetail detail)
        {
            EnsureNoDetailAttached();
            GenericDetail = detail;
        }

        public int AttachedDetailCount =>
            (AirTransportDetail is null ? 0 : 1)
            + (SeatDetail is null ? 0 : 1)
            + (BaggageDetail is null ? 0 : 1)
            + (MealDetail is null ? 0 : 1)
            + (LoungeDetail is null ? 0 : 1)
            + (HotelDetail is null ? 0 : 1)
            + (GroundTransportDetail is null ? 0 : 1)
            + (GenericDetail is null ? 0 : 1);

        private void EnsureNoDetailAttached()
        {
            if (AttachedDetailCount > 0)
                throw ExceptionFactory.ServiceAlreadyHasTypedDetail(Id, ServiceType);
        }

        internal void Activate()
        {
            if (Status == OrderServiceStatus.Pending)
            {
                Status = OrderServiceStatus.Active;
                CommercialStatus = OrderServiceCommercialStatus.Active;
            }
        }

        internal void MarkReservationConfirmed()
        {
            FulfillmentStatus = OrderFulfillmentStatus.Confirmed;

            if (Status is OrderServiceStatus.Pending or OrderServiceStatus.Active)
                Status = OrderServiceStatus.Fulfilled;
        }

        internal void MarkReservationReleased()
        {
            FulfillmentStatus = OrderFulfillmentStatus.Cancelled;
            HoldBatchId = null;
            SeatHoldReference = null;
        }

        internal void MarkDocumented(long electronicTicketId, long ticketCouponId)
        {
            DocumentStatus = OrderServiceDocumentStatus.Issued;
            ElectronicTicketId = electronicTicketId;
            TicketCouponId = ticketCouponId;
        }

        internal void MarkMiscellaneousDocumented(long electronicMiscDocumentId, long emdCouponId)
        {
            DocumentStatus = OrderServiceDocumentStatus.Issued;
            ElectronicMiscDocumentId = electronicMiscDocumentId;
            EmdCouponId = emdCouponId;
        }

        internal void AttachEmdIssuanceSnapshot(OrderServiceEmdIssuanceSnapshot snapshot)
            => EmdIssuanceSnapshot = snapshot;

        internal void MarkFulfilled(string? holdBatchId, string? seatHoldReference)
        {
            FulfillmentStatus = OrderFulfillmentStatus.Confirmed;
            HoldBatchId = holdBatchId;
            SeatHoldReference = seatHoldReference;
        }

        internal void MarkIssued(long trafficDocumentId, long documentCouponId)
        {
            DocumentStatus = OrderServiceDocumentStatus.Issued;
            TrafficDocumentId = trafficDocumentId;
            DocumentCouponId = documentCouponId;
        }

        internal void MarkVoided()
        {
            DocumentStatus = OrderServiceDocumentStatus.Voided;
            Status = OrderServiceStatus.Cancelled;
            CommercialStatus = OrderServiceCommercialStatus.Cancelled;
            DeliveryStatus = OrderServiceDeliveryStatus.Unused;
            FinancialStatus = OrderServiceFinancialStatus.Refunded;
        }

        internal OrderService CopyTo(
            long newId,
            long newOrderId,
            long newItemId,
            IReadOnlyDictionary<long, long> segmentMap,
            IReadOnlyDictionary<long, long> travellerMap,
            string? newHoldBatchId,
            Func<long> nextId)
        {
            var copy = new OrderService(new CreateOrderServiceArgs(
                newId,
                newOrderId,
                newItemId,
                ServiceType,
                ServiceCode,
                Name,
                DeliveryModel,
                PriceTreatment,
                RequiresReservation,
                RequiresSupplierConfirmation,
                RequiresDocument,
                ProviderType,
                CreatedAt,
                DocumentKind,
                RequiresPaymentCoverage,
                SupplierCode,
                DeliveryProviderReference))
            {
                Status = Status,
                CommercialStatus = CommercialStatus,
                FulfillmentStatus = FulfillmentStatus,
                DeliveryStatus = DeliveryStatus,
                FinancialStatus = FinancialStatus,
                DocumentStatus = DocumentStatus,
                HoldBatchId = newHoldBatchId,
                SeatHoldReference = SeatHoldReference,
                TrafficDocumentId = TrafficDocumentId,
                DocumentCouponId = DocumentCouponId,
                ElectronicTicketId = ElectronicTicketId,
                TicketCouponId = TicketCouponId
            };

            foreach (var beneficiary in _beneficiaries)
                if (travellerMap.TryGetValue(beneficiary.OrderTravellerId, out var mappedTraveller))
                    copy.AddBeneficiary(nextId(), mappedTraveller);

            if (AirTransportDetail is { } air && segmentMap.TryGetValue(air.OrderSegmentId, out var mappedSegment))
                copy.AttachAirTransport(new OrderAirTransportServiceDetail(
                    nextId(),
                    copy.Id,
                    mappedSegment,
                    air.TransitionalFareBasis,
                    air.RequestedSeat,
                    air.TransitionalCheckedBaggage?.Copy(),
                    air.TransitionalCabinBaggage?.Copy()));

            return copy;
        }

        internal void MarkCancelled()
        {
            if (DocumentStatus == OrderServiceDocumentStatus.Issued)
                DocumentStatus = OrderServiceDocumentStatus.Cancelled;

            Status = OrderServiceStatus.Cancelled;
            CommercialStatus = OrderServiceCommercialStatus.Cancelled;
            DeliveryStatus = OrderServiceDeliveryStatus.Unused;
            FinancialStatus = OrderServiceFinancialStatus.Refunded;
        }
    }
}
