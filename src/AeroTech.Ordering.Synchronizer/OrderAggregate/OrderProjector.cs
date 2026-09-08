using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Persistence;
using AeroTech.Ordering.Query.OrderAggregate.Models;
using AeroTech.Ordering.Query._Shared.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Synchronizer.OrderAggregate
{
    public sealed class OrderProjector : IOrderProjector
    {
        private static readonly JsonSerializerOptions SnapshotOptions = new() { WriteIndented = false };

        private readonly OrderingDbContext _commandDbContext;
        private readonly OrderQueryDbContext _queryDbContext;
        private readonly IClock _clock;

        public OrderProjector(OrderingDbContext commandDbContext, OrderQueryDbContext queryDbContext, IClock clock)
        {
            _commandDbContext = commandDbContext;
            _queryDbContext = queryDbContext;
            _clock = clock;
        }

        public async Task ProjectAsync(long orderId, CancellationToken cancellationToken = default)
        {
            var order = await _commandDbContext.Orders
                .Include(candidate => candidate.Travellers)
                .Include(candidate => candidate.Segments)
                .Include(candidate => candidate.Itineraries)
                .Include(candidate => candidate.Items).ThenInclude(item => item.ProductSnapshot)
                .Include(candidate => candidate.Items).ThenInclude(item => item.CommercialTermsSnapshot)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.Beneficiaries)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.CoveredServices)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.CoveredSegments)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.AirTransportDetail)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.SeatDetail)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.BaggageDetail)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.MealDetail)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.LoungeDetail)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.HotelDetail)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.GroundTransportDetail)
                .Include(candidate => candidate.OrderServices).ThenInclude(service => service.GenericDetail)
                .Include(candidate => candidate.ItemServiceLinks)
                .Include(candidate => candidate.Changes)
                .Include(candidate => candidate.PriceChangeSets)
                .Include(candidate => candidate.PricingLines).ThenInclude(line => line.AllocationSets).ThenInclude(set => set.Allocations)
                .Include(candidate => candidate.FareConstructions).ThenInclude(construction => construction.Items)
                .Include(candidate => candidate.FareConstructions).ThenInclude(construction => construction.PricingGroups).ThenInclude(group => group.Travellers)
                .Include(candidate => candidate.FareConstructions).ThenInclude(construction => construction.PricingGroups).ThenInclude(group => group.PricingUnits).ThenInclude(unit => unit.FareComponents).ThenInclude(component => component.Services)
                .Include(candidate => candidate.FareConstructions).ThenInclude(construction => construction.PricingGroups).ThenInclude(group => group.PricingUnits).ThenInclude(unit => unit.FareComponents).ThenInclude(component => component.Segments)
                .Include(candidate => candidate.TimeLimits)
                .Include(candidate => candidate.ExternalReferences)
                .AsSplitQuery()
                .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

            if (order is null)
                return;

            var reservations = await _commandDbContext.FulfillmentReservations
                .Include(reservation => reservation.Services)
                .Where(reservation => reservation.OrderId == orderId)
                .ToListAsync(cancellationToken);

            var tickets = await _commandDbContext.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .Where(ticket => ticket.CurrentServicingOrderId == orderId)
                .ToListAsync(cancellationToken);

            var miscDocuments = await _commandDbContext.ElectronicMiscDocuments
                .Include(document => document.Coupons)
                .Where(document => document.CurrentServicingOrderId == orderId)
                .ToListAsync(cancellationToken);

            var reservationSummary = RollUpReservation(reservations.Select(reservation => reservation.Status).ToList());
            var documentSummary = RollUpDocuments(order);

            await UpsertSearchAsync(order, reservationSummary, documentSummary, cancellationToken);
            await UpsertDetailsAsync(order, reservations, tickets, miscDocuments, reservationSummary, cancellationToken);
            await ReconcileTravellersAsync(order, cancellationToken);
            await ReconcileFlightsAsync(order, cancellationToken);
        }

        private async Task UpsertSearchAsync(
            Domain.OrderAggregate.Order order,
            FulfillmentReservationStatus? reservationSummary,
            OrderServiceDocumentStatus? documentSummary,
            CancellationToken cancellationToken)
        {
            var row = await _queryDbContext.Orders.SingleOrDefaultAsync(candidate => candidate.Id == order.Id, cancellationToken);

            if (row is null)
            {
                row = new OrderReadModel { Id = order.Id };
                _queryDbContext.Orders.Add(row);
            }

            row.UniqueIdentifierId = order.UniqueIdentifierId;
            row.RecordLocator = order.RecordLocator?.Value;
            row.Status = order.Status;
            row.Type = order.Type;
            row.Channel = order.Channel;
            row.CustomerId = order.CustomerId;
            row.AirlineOfficeId = order.AirlineOfficeId;
            row.CreatorUserId = order.CreatorUserId;
            row.CurrencyId = order.CurrencyId;
            row.Pax = order.Pax;
            row.GrandTotal = order.Amount.GrandTotal;
            row.TotalTax = order.Amount.TaxTotal;
            row.CommissionAmount = order.Commission.CommissionAmount;
            row.CommissionRate = order.Commission.CommissionRate;
            row.CommercialVersion = order.CommercialVersion;
            row.CommercialSummary = order.CommercialSummary;
            row.ReservationSummary = reservationSummary;
            row.DocumentSummary = documentSummary;
            row.LinkedOrderId = order.LinkedOrderId;
            row.LinkedPNR = order.LinkedPNR;
            row.TimeToLive = order.TimeToLive;
            row.CreationDate = order.CreationDate;
            row.LastProjectedAt = _clock.GetDateTime();
            row.ProjectionRevision++;
        }

        private async Task UpsertDetailsAsync(
            Domain.OrderAggregate.Order order,
            IReadOnlyList<Domain.FulfillmentReservationAggregate.FulfillmentReservation> reservations,
            IReadOnlyList<Domain.ElectronicTicketAggregate.ElectronicTicket> tickets,
            IReadOnlyList<Domain.ElectronicMiscDocumentAggregate.ElectronicMiscDocument> miscDocuments,
            FulfillmentReservationStatus? reservationSummary,
            CancellationToken cancellationToken)
        {
            var row = await _queryDbContext.OrderDetails.SingleOrDefaultAsync(candidate => candidate.Id == order.Id, cancellationToken);

            if (row is null)
            {
                row = new OrderDetailsReadModel { Id = order.Id };
                _queryDbContext.OrderDetails.Add(row);
            }

            row.ProjectionRevision++;
            row.UpdatedAt = _clock.GetDateTime();

            var view = OrderViewBuilder.Build(
                order,
                reservations,
                tickets,
                miscDocuments,
                reservationSummary,
                row.ProjectionRevision,
                row.UpdatedAt);

            row.SnapshotJson = JsonSerializer.Serialize(view, SnapshotOptions);
        }

        private async Task ReconcileTravellersAsync(Domain.OrderAggregate.Order order, CancellationToken cancellationToken)
        {
            var existing = await _queryDbContext.OrderTravellers
                .Where(traveller => traveller.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            foreach (var traveller in order.Travellers)
            {
                var row = existing.SingleOrDefault(candidate => candidate.Id == traveller.Id);

                if (row is null)
                {
                    row = new OrderTravellerReadModel { Id = traveller.Id, OrderId = order.Id };
                    _queryDbContext.OrderTravellers.Add(row);
                }

                row.Index = traveller.Index;
                row.FirstName = traveller.Name.FirstName;
                row.SurName = traveller.Name.SurName;
                row.AgeRange = traveller.AgeRange;
            }

            var incoming = order.Travellers.Select(traveller => traveller.Id).ToHashSet();

            foreach (var stale in existing.Where(row => !incoming.Contains(row.Id)))
                _queryDbContext.OrderTravellers.Remove(stale);
        }

        private async Task ReconcileFlightsAsync(Domain.OrderAggregate.Order order, CancellationToken cancellationToken)
        {
            var existing = await _queryDbContext.OrderFlights
                .Where(flight => flight.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            foreach (var segment in order.Segments)
            {
                var row = existing.SingleOrDefault(candidate => candidate.Id == segment.Id);

                if (row is null)
                {
                    row = new OrderFlightReadModel { Id = segment.Id, OrderId = order.Id };
                    _queryDbContext.OrderFlights.Add(row);
                }

                row.Sequence = segment.Sequence;
                row.FlightNumber = segment.Number;
                row.MarketingAirlineId = segment.MarketingAirlineId;
                row.OriginAirportId = segment.OriginAirportId;
                row.DestinationAirportId = segment.DestinationAirportId;
                row.DepartureDateTime = segment.DepartureDateTime;
                row.ArrivalDateTime = segment.ArrivalDateTime;
            }

            var incoming = order.Segments.Select(segment => segment.Id).ToHashSet();

            foreach (var stale in existing.Where(row => !incoming.Contains(row.Id)))
                _queryDbContext.OrderFlights.Remove(stale);
        }

        private static FulfillmentReservationStatus? RollUpReservation(IReadOnlyList<FulfillmentReservationStatus> statuses)
        {
            if (statuses.Count == 0)
                return null;

            return statuses.Distinct().Count() == 1 ? statuses[0] : FulfillmentReservationStatus.Mixed;
        }

        private static OrderServiceDocumentStatus? RollUpDocuments(Domain.OrderAggregate.Order order)
        {
            var required = order.RequiredDocumentServiceIds();

            if (required.Count == 0)
                return null;

            var documented = order.DocumentedServiceIds();

            if (documented.Count == 0)
                return OrderServiceDocumentStatus.Pending;

            return required.All(documented.Contains)
                ? OrderServiceDocumentStatus.Issued
                : OrderServiceDocumentStatus.Pending;
        }

    }
}
