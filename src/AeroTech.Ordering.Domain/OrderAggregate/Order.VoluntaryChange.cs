using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public StagedVoluntaryChange PrepareVoluntaryChange(
            AcceptedVoluntaryChangeArgs args,
            IIdGenerator idGenerator,
            IClock clock)
            => StageVoluntaryChange(args, idGenerator, clock.GetDateTime());

        public ChangedServices CommitVoluntaryChange(
            StagedVoluntaryChange staged,
            IClock clock)
            => AttachVoluntaryChange(staged, clock.GetDateTime());

        public OrderService RequireChangeableAirService(long orderServiceId)
        {
            var service = _orderServices.FirstOrDefault(candidate => candidate.Id == orderServiceId)
                          ?? throw ExceptionFactory.ChangeScopeServiceNotInOrder(orderServiceId, Id);

            if (!service.IsAirTransport)
                throw ExceptionFactory.ChangeScopeServiceNotChangeable(orderServiceId, service.ServiceType);

            if (service.Status is OrderServiceStatus.Cancelled or OrderServiceStatus.Failed)
                throw ExceptionFactory.ChangeScopeServiceNotChangeable(orderServiceId, service.Status);

            if (service.DocumentStatus != OrderServiceDocumentStatus.Issued)
                throw ExceptionFactory.ChangeScopeServiceNotChangeable(orderServiceId, service.DocumentStatus);

            return service;
        }

        public void EnsureNoActiveServiceDependsOn(long orderServiceId)
        {
            foreach (var service in _orderServices.Where(candidate =>
                         candidate.Id != orderServiceId
                         && candidate.Status is not (OrderServiceStatus.Cancelled or OrderServiceStatus.Failed)))
            {
                if (ServiceDependencyPolicy.DependenciesOf(service).Contains(orderServiceId))
                    throw ExceptionFactory.ChangeWouldOrphanDependentService(service.Id, orderServiceId);
            }
        }

        private StagedVoluntaryChange StageVoluntaryChange(
            AcceptedVoluntaryChangeArgs args,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(args);

            var accepted = args.Accepted;

            if (accepted.MonetaryOutcome != ChangeMonetaryOutcome.Even)
                throw ExceptionFactory.ChangeMonetaryOutcomeNotSupported(
                    accepted.QuotedChangeId, accepted.MonetaryOutcome);

            var replaced = RequireChangeableAirService(accepted.ReplacedOrderServiceId);

            EnsureNoActiveServiceDependsOn(replaced.Id);
            EnsureContinuedServicesKeepTheirIdentity(accepted);

            foreach (var travellerId in accepted.Replacement.BeneficiaryTravellerIds)
            {
                if (_travellers.All(traveller => traveller.Id != travellerId))
                    throw ExceptionFactory.ChangeReplacementTravellerNotInOrder(travellerId, Id);
            }

            if (accepted.Replacement.BeneficiaryTravellerIds.Count == 0)
                throw ExceptionFactory.ServiceRequiresBeneficiary(accepted.Replacement.ServiceRef);

            var itineraryId = _segments
                .Single(segment => segment.Id == replaced.SoldSegmentId!.Value)
                .OrderItineraryId;

            var segment = StageReplacementSegment(
                accepted.Replacement.Segment, args.ReplacementOrderSegmentId, itineraryId, idGenerator);

            var service = StageReplacementService(
                accepted, args.ReplacementOrderServiceId, replaced, segment.Id, idGenerator, now);

            var change = StageOrderChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.VoluntaryChange,
                    PriceChangeReason.Correction,
                    accepted.PricingSource,
                    [],
                    SourcePricingRef: accepted.SourcePricingReference,
                    ChangeReason: accepted.QuotedChangeId,
                    ExternalReference: accepted.TargetSelectionRef,
                    ActorScope: args.ActorScope,
                    ActorId: args.ActorId,
                    OperationId: args.OperationId),
                idGenerator,
                now);

            return new StagedVoluntaryChange(
                change,
                segment,
                service,
                replaced.Id,
                accepted.ElectronicTicketId,
                accepted.ReplacedTicketCouponId);
        }

        private void EnsureContinuedServicesKeepTheirIdentity(AcceptedVoluntaryChange accepted)
        {
            foreach (var continuedId in accepted.ContinuedOrderServiceIds)
            {
                if (continuedId == accepted.ReplacedOrderServiceId)
                    throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("continued service scope");

                if (_orderServices.All(service => service.Id != continuedId))
                    throw ExceptionFactory.ChangeScopeServiceNotInOrder(continuedId, Id);
            }
        }

        private OrderSegment StageReplacementSegment(
            AcceptedSegment accepted,
            long segmentId,
            long itineraryId,
            IIdGenerator idGenerator)
        {
            var segment = new OrderSegment(Id, new CreateOrderSegmentArgs(
                segmentId,
                itineraryId,
                accepted.Sequence,
                accepted.CabinClassId,
                accepted.RbdId,
                accepted.BookingClassCode,
                accepted.CapacityReference,
                accepted.BookingClass,
                accepted.FareReference,
                accepted.FlightSourceId,
                accepted.FlightVersion,
                accepted.FlightNumber,
                accepted.OriginAirportId,
                accepted.OriginAirportTerminalId,
                accepted.DestinationAirportId,
                accepted.DestinationAirportTerminalId,
                accepted.OperatingAirlineId,
                accepted.MarketingAirlineId,
                accepted.DepartureAt,
                accepted.ArrivalAt,
                accepted.DurationMinutes,
                accepted.AircraftId));

            foreach (var leg in accepted.Legs.OrderBy(leg => leg.Sequence))
                segment.AddLeg(new CreateOrderSegmentLegArgs(
                    idGenerator.NewId(),
                    leg.Sequence,
                    leg.LegSourceId,
                    leg.OriginAirportId,
                    leg.OriginAirportTerminalId,
                    leg.DestinationAirportId,
                    leg.DestinationAirportTerminalId,
                    leg.DepartureAt,
                    leg.ArrivalAt,
                    null,
                    null));

            return segment;
        }

        private OrderService StageReplacementService(
            AcceptedVoluntaryChange accepted,
            long serviceId,
            OrderService replaced,
            long segmentId,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            var replacement = accepted.Replacement;

            var service = new OrderService(new CreateOrderServiceArgs(
                serviceId,
                Id,
                replaced.OrderItemId,
                OrderServiceType.AirTransportation,
                replacement.ServiceCode,
                replacement.Name,
                replaced.DeliveryModel,
                replaced.PriceTreatment,
                replaced.RequiresReservation,
                replaced.RequiresSupplierConfirmation,
                replaced.RequiresDocument,
                replaced.ProviderType,
                now,
                replaced.DocumentKind,
                replaced.RequiresPaymentCoverage,
                replaced.SupplierCode,
                replaced.DeliveryProviderReference));

            foreach (var travellerId in replacement.BeneficiaryTravellerIds)
                service.AddBeneficiary(idGenerator.NewId(), travellerId);

            service.AttachAirTransport(new OrderAirTransportServiceDetail(
                idGenerator.NewId(),
                service.Id,
                segmentId,
                replacement.Detail.TransitionalFareBasis,
                replacement.Detail.RequestedSeat));

            return service;
        }

        private ChangedServices AttachVoluntaryChange(StagedVoluntaryChange staged, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(staged);

            AddSegment(staged.Segment);
            AddOrderService(staged.Service);
            _changes.Add(staged.Change);

            var replaced = _orderServices.Single(service => service.Id == staged.ReplacedOrderServiceId);

            replaced.MarkSupersededByVoluntaryChange();

            staged.Service.Activate();
            staged.Service.MarkReservationConfirmed();
            staged.Service.MarkDocumented(staged.ElectronicTicketId, staged.TicketCouponId);

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            _ = now;

            return new ChangedServices(
                staged.Change.Id,
                staged.ReplacedOrderServiceId,
                staged.Service.Id,
                staged.Segment.Id,
                staged.ElectronicTicketId,
                staged.TicketCouponId);
        }
    }
}
