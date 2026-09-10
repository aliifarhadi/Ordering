using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain.Ports.FlightFlow;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void RequestReservation() => EnsureCanReserve();

        private void EnsureCanReserve()
        {
            if (Status != OrderStatus.Created)
                throw ExceptionFactory.OrderCannotBeReserved(Id, Status);
        }

        public void FailReservation(string reason, IIdGenerator idGenerator, IClock clock)
        {
            TransitionTo(OrderStatus.ReserveFailed);

            Causes(new OrderReserveFailed(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                Status,
                reason));
        }

        public void MarkReservationUnconfirmed(FulfillmentFailureReason reason, string detail, IIdGenerator idGenerator, IClock clock)
        {
            ReservationFailureReason = reason;

            TransitionTo(OrderStatus.ReservationUnconfirmed);

            Causes(new OrderReservationUnconfirmed(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                CustomerId,
                AirlineOfficeId,
                Status,
                reason,
                detail));
        }

        public void CompleteReserve(
            string recordLocator,
            DateTimeOffset? timeToLive,
            IReadOnlyList<ReservedServiceLink> serviceLinks,
            IIdGenerator idGenerator,
            IClock clock)
        {
            RecordLocator = new RecordLocator(recordLocator);
            TimeToLive = timeToLive;

            foreach (var service in _orderServices)
            {
                var link = serviceLinks.FirstOrDefault(candidate => candidate.OrderServiceId == service.Id);
                if (link is not null)
                    service.MarkFulfilled(link.HoldBatchId, link.SeatHoldReference);
            }

            TransitionTo(OrderStatus.Confirmed);

            Causes(new OrderReserved(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                recordLocator,
                Status,
                CustomerId,
                AirlineOfficeId,
                Channel,
                CurrencyId,
                Pax,
                Amount.GrandTotal,
                Amount.TaxTotal,
                CreationDate));
        }

        public HoldSeatsRequest BuildReserveHoldRequest(string idempotencyKey, DateTimeOffset expiresAt)
        {
            var holdableTravellers = _travellers.Where(traveller => traveller.AgeRange != AgeRange.Infant).ToList();
            var holdableTravellerIds = holdableTravellers.Select(traveller => traveller.Id).ToHashSet();

            var passengers = holdableTravellers
                .Select(traveller => new PassengerForHoldSeatRequest(
                    traveller.Id.ToString(),
                    traveller.PassengerType,
                    traveller.Gender))
                .ToList();

            var flights = _orderServices
                .Where(service => service.IsAirTransport && service.RequiresReservation
                                  && holdableTravellerIds.Contains(service.SoleBeneficiaryId))
                .GroupBy(service => service.SoldSegmentId!.Value)
                .Select(group =>
                {
                    var segment = _segments.Single(candidate => candidate.Id == group.Key);
                    var seats = group
                        .Select(service => new SeatForHoldSeatRequest(
                            service.SoleBeneficiaryId.ToString(),
                            0m,
                            service.AirTransportDetail?.RequestedSeat))
                        .ToList();
                    return new FlightForHoldSeatRequest(segment.FlightCapacityId.ToString(), seats);
                })
                .Where(flight => flight.Seats.Count > 0)
                .ToList();

            return new HoldSeatsRequest(idempotencyKey, UniqueIdentifierId.ToString(), expiresAt, passengers, flights);
        }
    }
}
