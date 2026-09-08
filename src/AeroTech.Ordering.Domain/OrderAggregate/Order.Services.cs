using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        private static readonly OrderServiceType[] BlockedServiceTypes =
        [
            OrderServiceType.Penalty,
            OrderServiceType.ServiceFee,
            OrderServiceType.Credit,
            OrderServiceType.Voucher,
            OrderServiceType.TaxAdjustment,
            OrderServiceType.ManualAdjustment,
            OrderServiceType.Notification,
            OrderServiceType.TransferRide
        ];

        public IEnumerable<OrderService> AirTransportServices =>
            _orderServices.Where(service => service.IsAirTransport);

        public IReadOnlyCollection<OrderItemServiceLink> ItemServiceLinks => _itemServiceLinks.AsReadOnly();

        public IEnumerable<OrderItemServiceLink> OriginalItemMembership(long orderServiceId)
            => _itemServiceLinks.Where(link => link.OrderServiceId == orderServiceId);

        internal OrderService BuildService(
            AcceptedService accepted,
            long orderItemId,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            EnsureServiceTypeIsAcceptable(accepted.ServiceType);

            var profile = ResolveFulfillmentProfile(accepted);

            var service = new OrderService(new CreateOrderServiceArgs(
                idGenerator.NewId(),
                Id,
                orderItemId,
                accepted.ServiceType,
                accepted.ServiceCode,
                accepted.Name,
                accepted.DeliveryModel,
                accepted.PriceTreatment,
                profile.RequiresReservation,
                accepted.RequiresSupplierConfirmation,
                profile.RequiresDocument,
                accepted.ProviderType,
                now,
                profile.DocumentKind,
                accepted.RequiresPaymentCoverage,
                accepted.SupplierCode,
                accepted.DeliveryProviderReference));

            AttachBeneficiaries(service, accepted, refs, idGenerator);
            AttachDetail(service, accepted, refs, idGenerator);
            AttachCoverage(service, accepted, refs, idGenerator);

            AddOrderService(service);
            refs.ServiceIds[accepted.ServiceRef] = service.Id;

            return service;
        }

        internal void LinkAcceptedServicesToItems(
            AcceptedOrderSource source,
            long changeId,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            foreach (var product in source.Products)
            {
                if (!refs.ProductItemIds.TryGetValue(product.ProductRef, out var orderItemId))
                    continue;

                foreach (var accepted in product.Services)
                {
                    if (!refs.ServiceIds.TryGetValue(accepted.ServiceRef, out var serviceId))
                        continue;

                    _itemServiceLinks.Add(new OrderItemServiceLink(
                        idGenerator.NewId(),
                        Id,
                        orderItemId,
                        serviceId,
                        changeId,
                        now));
                }
            }
        }

        private static void EnsureServiceTypeIsAcceptable(OrderServiceType serviceType)
        {
            if (BlockedServiceTypes.Contains(serviceType))
                throw ExceptionFactory.ServiceTypeNotSellable(serviceType);
        }

        private static (bool RequiresReservation, bool RequiresDocument, ServiceDocumentKind? DocumentKind) ResolveFulfillmentProfile(
            AcceptedService accepted)
        {
            if (accepted.Detail is not AcceptedGenericServiceDetail generic)
                return (accepted.RequiresReservation, accepted.RequiresDocument, accepted.DocumentKind);

            var schema = GenericServiceSchemaRegistry.Resolve(generic.SchemaName, generic.SchemaVersion);

            EnsureDetailMatchesType(accepted.ServiceType, schema.ServiceType);
            GenericServiceSchemaRegistry.EnsureAttributesAreValid(schema, generic.AttributesJson);

            return (schema.RequiresReservation, schema.RequiresDocument, schema.DocumentKind);
        }

        private void AttachBeneficiaries(
            OrderService service,
            AcceptedService accepted,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator)
        {
            if (accepted.BeneficiaryTravellerRefs.Count == 0)
                throw ExceptionFactory.ServiceRequiresBeneficiary(accepted.ServiceRef);

            foreach (var travellerRef in accepted.BeneficiaryTravellerRefs)
            {
                if (!refs.TravellerIds.TryGetValue(travellerRef, out var travellerId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("service beneficiary", travellerRef);

                if (_travellers.All(traveller => traveller.Id != travellerId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("service beneficiary", travellerRef);

                service.AddBeneficiary(idGenerator.NewId(), travellerId);
            }

            if (RequiresExactlyOneBeneficiary(accepted.ServiceType) && service.Beneficiaries.Count != 1)
                throw ExceptionFactory.ServiceRequiresExactlyOneBeneficiary(service.Id, service.Beneficiaries.Count);
        }

        private static bool RequiresExactlyOneBeneficiary(OrderServiceType serviceType)
            => serviceType is OrderServiceType.AirTransportation or OrderServiceType.SeatAssignment;

        private void AttachCoverage(
            OrderService service,
            AcceptedService accepted,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator)
        {
            foreach (var serviceRef in accepted.CoveredAirServiceRefs ?? [])
            {
                if (!refs.ServiceIds.TryGetValue(serviceRef, out var coveredId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("covered air service", serviceRef);

                var covered = _orderServices.FirstOrDefault(candidate => candidate.Id == coveredId);

                if (covered is null || !covered.IsAirTransport)
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("covered air service", serviceRef);

                service.CoverService(idGenerator.NewId(), coveredId);
            }

            foreach (var segmentRef in accepted.CoveredSegmentRefs ?? [])
            {
                if (!refs.SegmentIds.TryGetValue(segmentRef, out var segmentId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("covered segment", segmentRef);

                if (_segments.All(segment => segment.Id != segmentId))
                    throw ExceptionFactory.AcceptedSourceReferenceNotResolved("covered segment", segmentRef);

                service.CoverSegment(idGenerator.NewId(), segmentId);
            }
        }

        private void AttachDetail(
            OrderService service,
            AcceptedService accepted,
            AcceptedSourceRefMap refs,
            IIdGenerator idGenerator)
        {
            switch (accepted.Detail)
            {
                case AcceptedAirTransportDetail air:
                    EnsureDetailMatchesType(accepted.ServiceType, OrderServiceType.AirTransportation);
                    service.AttachAirTransport(new OrderAirTransportServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        ResolveSegment(air.SegmentRef, refs),
                        air.TransitionalFareBasis,
                        air.RequestedSeat,
                        BaggageOf(air.TransitionalCheckedBaggage),
                        BaggageOf(air.TransitionalCabinBaggage)));
                    break;

                case AcceptedSeatDetail seat:
                    EnsureDetailMatchesType(accepted.ServiceType, OrderServiceType.SeatAssignment);
                    service.AttachSeat(new OrderSeatServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        ResolveAirService(seat.AirServiceRef, refs),
                        seat.SoldSeatNumber));
                    break;

                case AcceptedBaggageDetail baggage:
                    EnsureDetailMatchesType(accepted.ServiceType, OrderServiceType.BaggageAllowance);
                    service.AttachBaggage(new OrderBaggageServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        baggage.Kind,
                        baggage.Pieces,
                        baggage.Weight,
                        baggage.WeightUnit,
                        baggage.PerPieceWeightLimit));
                    break;

                case AcceptedMealDetail meal:
                    EnsureDetailMatchesType(accepted.ServiceType, OrderServiceType.Meal);
                    service.AttachMeal(new OrderMealServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        meal.MealCode,
                        meal.Quantity,
                        meal.SpecialMealCode));
                    break;

                case AcceptedLoungeDetail lounge:
                    EnsureDetailMatchesType(accepted.ServiceType, OrderServiceType.LoungeAccess);
                    service.AttachLounge(new OrderLoungeServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        lounge.AirportId,
                        lounge.LoungeCode,
                        lounge.AccessStart,
                        lounge.AccessEnd,
                        lounge.GuestCount,
                        lounge.RelatedAirServiceRef is { } relatedRef ? ResolveAirService(relatedRef, refs) : null));
                    break;

                case AcceptedHotelDetail hotel:
                    EnsureDetailMatchesType(accepted.ServiceType, OrderServiceType.HotelStay);
                    service.AttachHotel(new OrderHotelServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        hotel.PropertyReference,
                        hotel.CheckIn,
                        hotel.CheckOut,
                        hotel.RoomCount,
                        hotel.GuestCount,
                        hotel.SupplierReference,
                        hotel.RoomTypeCode,
                        hotel.RatePlanReference));
                    break;

                case AcceptedGroundTransportDetail ground:
                    EnsureDetailMatchesType(accepted.ServiceType, OrderServiceType.GroundTransport);
                    service.AttachGroundTransport(new OrderGroundTransportServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        ground.PickupLocationReference,
                        ground.DropoffLocationReference,
                        ground.PickupAt,
                        ground.PassengerCount,
                        ground.VehicleTypeCode));
                    break;

                case AcceptedGenericServiceDetail generic:
                    service.AttachGeneric(new OrderGenericServiceDetail(
                        idGenerator.NewId(),
                        service.Id,
                        generic.SchemaName,
                        generic.SchemaVersion,
                        generic.AttributesJson));
                    break;

                default:
                    throw ExceptionFactory.ServiceDetailNotSupported(accepted.ServiceType);
            }
        }

        private static ValueObjects.Baggage? BaggageOf(AcceptedBaggageAllowance? allowance)
            => allowance is null ? null : new ValueObjects.Baggage(allowance.Weight, allowance.Unit, allowance.Pieces);

        private static void EnsureDetailMatchesType(OrderServiceType actual, OrderServiceType expected)
        {
            if (actual != expected)
                throw ExceptionFactory.ServiceDetailDoesNotMatchType(actual, expected);
        }

        private long ResolveSegment(string segmentRef, AcceptedSourceRefMap refs)
        {
            if (!refs.SegmentIds.TryGetValue(segmentRef, out var segmentId) || _segments.All(segment => segment.Id != segmentId))
                throw ExceptionFactory.AcceptedSourceReferenceNotResolved("segment", segmentRef);

            return segmentId;
        }

        private long ResolveAirService(string serviceRef, AcceptedSourceRefMap refs)
        {
            if (!refs.ServiceIds.TryGetValue(serviceRef, out var serviceId))
                throw ExceptionFactory.AcceptedSourceReferenceNotResolved("associated air service", serviceRef);

            var candidate = _orderServices.FirstOrDefault(service => service.Id == serviceId);

            if (candidate is null || !candidate.IsAirTransport)
                throw ExceptionFactory.AcceptedSourceReferenceNotResolved("associated air service", serviceRef);

            return serviceId;
        }
    }
}
