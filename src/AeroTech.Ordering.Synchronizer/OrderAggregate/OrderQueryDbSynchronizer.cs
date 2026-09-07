using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Query._Shared.DbContexts;
using AeroTech.Ordering.Query.OrderAggregate.Models;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Synchronizer.OrderAggregate
{
    public sealed class OrderQueryDbSynchronizer : IOrderQueryDbSynchronizer
    {
        private readonly OrderQueryDbContext _dbContext;

        public OrderQueryDbSynchronizer(OrderQueryDbContext dbContext) => _dbContext = dbContext;

        public Task ProjectCreatedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectReservedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectReservationUnconfirmedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectReserveFailedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectPaidAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectPaymentFailedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectPaymentUnconfirmedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectTicketedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectTicketingFailedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectVoidedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectCancelledAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectExpiredAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectSplitAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        public Task ProjectTimeToLiveUpdatedAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken = default)
            => UpsertAsync(snapshot, cancellationToken);

        private async Task UpsertAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.Orders
                .FirstOrDefaultAsync(order => order.Id == snapshot.OrderId, cancellationToken);

            if (existing is null)
            {
                _dbContext.Orders.Add(Map(snapshot, new OrderReadModel { Id = snapshot.OrderId }));
                await ReconcileTravellersAsync(snapshot, cancellationToken);
                await ReconcileFlightsAsync(snapshot, cancellationToken);
                return;
            }

            if (snapshot.OccurredAt <= existing.LastProjectedAt)
                return;

            Map(snapshot, existing);
            await ReconcileTravellersAsync(snapshot, cancellationToken);
            await ReconcileFlightsAsync(snapshot, cancellationToken);
        }

        private async Task ReconcileTravellersAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.OrderTravellers
                .Where(traveller => traveller.OrderId == snapshot.OrderId)
                .ToListAsync(cancellationToken);
            var existingById = existing.ToDictionary(traveller => traveller.Id);
            var incomingIds = snapshot.Travellers.Select(traveller => traveller.TravellerId).ToHashSet();

            foreach (var traveller in snapshot.Travellers)
            {
                if (existingById.TryGetValue(traveller.TravellerId, out var row))
                {
                    row.Index = traveller.Index;
                    row.FirstName = traveller.FirstName;
                    row.SurName = traveller.SurName;
                    row.AgeRange = traveller.AgeRange;
                }
                else
                {
                    _dbContext.OrderTravellers.Add(new OrderTravellerReadModel
                    {
                        Id = traveller.TravellerId,
                        OrderId = snapshot.OrderId,
                        Index = traveller.Index,
                        FirstName = traveller.FirstName,
                        SurName = traveller.SurName,
                        AgeRange = traveller.AgeRange
                    });
                }
            }

            foreach (var row in existing)
                if (!incomingIds.Contains(row.Id))
                    _dbContext.OrderTravellers.Remove(row);
        }

        private async Task ReconcileFlightsAsync(OrderReadModelSnapshot snapshot, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.OrderFlights
                .Where(flight => flight.OrderId == snapshot.OrderId)
                .ToListAsync(cancellationToken);
            var existingById = existing.ToDictionary(flight => flight.Id);
            var incomingIds = snapshot.Flights.Select(flight => flight.SegmentId).ToHashSet();

            foreach (var flight in snapshot.Flights)
            {
                if (existingById.TryGetValue(flight.SegmentId, out var row))
                {
                    row.Sequence = flight.Sequence;
                    row.FlightNumber = flight.FlightNumber;
                    row.MarketingAirlineId = flight.MarketingAirlineId;
                    row.OriginAirportId = flight.OriginAirportId;
                    row.DestinationAirportId = flight.DestinationAirportId;
                    row.DepartureDateTime = flight.DepartureDateTime;
                    row.ArrivalDateTime = flight.ArrivalDateTime;
                }
                else
                {
                    _dbContext.OrderFlights.Add(new OrderFlightReadModel
                    {
                        Id = flight.SegmentId,
                        OrderId = snapshot.OrderId,
                        Sequence = flight.Sequence,
                        FlightNumber = flight.FlightNumber,
                        MarketingAirlineId = flight.MarketingAirlineId,
                        OriginAirportId = flight.OriginAirportId,
                        DestinationAirportId = flight.DestinationAirportId,
                        DepartureDateTime = flight.DepartureDateTime,
                        ArrivalDateTime = flight.ArrivalDateTime
                    });
                }
            }

            foreach (var row in existing)
                if (!incomingIds.Contains(row.Id))
                    _dbContext.OrderFlights.Remove(row);
        }

        private static OrderReadModel Map(OrderReadModelSnapshot snapshot, OrderReadModel target)
        {
            target.UniqueIdentifierId = snapshot.UniqueIdentifierId;
            target.RecordLocator = snapshot.RecordLocator;
            target.Status = snapshot.Status;
            target.Type = snapshot.Type;
            target.Channel = snapshot.Channel;
            target.CustomerId = snapshot.CustomerId;
            target.AirlineOfficeId = snapshot.AirlineOfficeId;
            target.CreatorUserId = snapshot.CreatorUserId;
            target.CurrencyId = snapshot.CurrencyId;
            target.Pax = snapshot.Pax;
            target.GrandTotal = snapshot.GrandTotal;
            target.TotalTax = snapshot.TotalTax;
            target.CommissionAmount = snapshot.CommissionAmount;
            target.CommissionRate = snapshot.CommissionRate;
            target.CommercialVersion = snapshot.CommercialVersion;
            target.LinkedOrderId = snapshot.LinkedOrderId;
            target.LinkedPNR = snapshot.LinkedPNR;
            target.TimeToLive = snapshot.TimeToLive;
            target.CreationDate = snapshot.CreationDate;
            target.LastProjectedAt = snapshot.OccurredAt;
            return target;
        }
    }
}
