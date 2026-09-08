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
                .Include(candidate => candidate.Items)
                .Include(candidate => candidate.OrderServices)
                .Include(candidate => candidate.PricingLines)
                .Include(candidate => candidate.TimeLimits)
                .Include(candidate => candidate.ExternalReferences)
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

            var reservationSummary = RollUpReservation(reservations.Select(reservation => reservation.Status).ToList());
            var documentSummary = RollUpDocuments(order);

            var snapshot = BuildSnapshot(order, reservations, tickets, reservationSummary, documentSummary);

            await UpsertSearchAsync(order, reservationSummary, documentSummary, cancellationToken);
            await UpsertDetailsAsync(order, snapshot, cancellationToken);
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
            object snapshot,
            CancellationToken cancellationToken)
        {
            var row = await _queryDbContext.OrderDetails.SingleOrDefaultAsync(candidate => candidate.Id == order.Id, cancellationToken);

            if (row is null)
            {
                row = new OrderDetailsReadModel { Id = order.Id };
                _queryDbContext.OrderDetails.Add(row);
            }

            row.SnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotOptions);
            row.UpdatedAt = _clock.GetDateTime();
            row.ProjectionRevision++;
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

        private static object BuildSnapshot(
            Domain.OrderAggregate.Order order,
            IReadOnlyList<Domain.FulfillmentReservationAggregate.FulfillmentReservation> reservations,
            IReadOnlyList<Domain.ElectronicTicketAggregate.ElectronicTicket> tickets,
            FulfillmentReservationStatus? reservationSummary,
            OrderServiceDocumentStatus? documentSummary)
            => new
            {
                order.Id,
                order.UniqueIdentifierId,
                RecordLocator = order.RecordLocator?.Value,
                order.CommercialVersion,
                order.CommercialSummary,
                order.ObligationVersion,
                order.CurrencyId,
                order.CustomerId,
                order.AirlineOfficeId,
                order.Channel,
                order.Pax,
                order.CreationDate,
                order.TimeToLive,
                Totals = new
                {
                    order.Amount.GrandTotal,
                    order.Amount.BaseFareTotal,
                    order.Amount.TaxTotal,
                    order.Amount.FeeTotal
                },
                Facets = new
                {
                    Commercial = order.CommercialSummary,
                    Reservation = reservationSummary,
                    Document = new
                    {
                        Status = documentSummary,
                        RequiredServices = order.RequiredDocumentServiceIds().Count,
                        DocumentedServices = order.DocumentedServiceIds().Count
                    }
                },
                Travelers = order.Travellers
                    .OrderBy(traveller => traveller.Index)
                    .Select(traveller => new
                    {
                        traveller.Id,
                        traveller.Index,
                        traveller.Name.FirstName,
                        traveller.Name.SurName,
                        traveller.AgeRange,
                        traveller.PassengerType
                    }),
                Journeys = order.Itineraries
                    .OrderBy(itinerary => itinerary.Sequence)
                    .Select(itinerary => new
                    {
                        itinerary.Id,
                        itinerary.Sequence,
                        itinerary.OriginAirportId,
                        itinerary.DestinationAirportId,
                        Segments = order.Segments
                            .Where(segment => segment.OrderItineraryId == itinerary.Id)
                            .OrderBy(segment => segment.Sequence)
                            .Select(segment => new
                            {
                                segment.Id,
                                segment.Sequence,
                                segment.Number,
                                segment.MarketingAirlineId,
                                segment.OperatingAirlineId,
                                segment.OriginAirportId,
                                segment.DestinationAirportId,
                                segment.DepartureDateTime,
                                segment.ArrivalDateTime,
                                segment.BookingClass
                            })
                    }),
                Items = order.Items.Select(item => new
                {
                    item.Id,
                    item.ProductType,
                    item.ProductCode,
                    item.CommercialStatus,
                    Services = order.OrderServices
                        .Where(service => service.OrderItemId == item.Id)
                        .Select(service => new
                        {
                            service.Id,
                            service.ServiceType,
                            service.ServiceCode,
                            service.Name,
                            service.Status,
                            service.CommercialStatus,
                            service.FulfillmentStatus,
                            service.DeliveryStatus,
                            service.DocumentStatus,
                            service.PriceTreatment,
                            CurrentItemId = service.OrderItemId,
                            OriginalItemIds = order.ItemServiceLinks
                                .Where(link => link.OrderServiceId == service.Id)
                                .Select(link => link.OrderItemId)
                                .ToList(),
                            Beneficiaries = service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToList(),
                            Fulfillment = new
                            {
                                service.RequiresReservation,
                                service.RequiresSupplierConfirmation,
                                service.RequiresDocument,
                                service.DocumentKind,
                                service.RequiresPaymentCoverage,
                                service.ProviderType,
                                service.SupplierCode
                            },
                            Coverage = new
                            {
                                Services = service.CoveredServices.Select(covered => covered.CoveredOrderServiceId).ToList(),
                                Segments = service.CoveredSegments.Select(covered => covered.OrderSegmentId).ToList()
                            },
                            Detail = DescribeServiceDetail(service),
                            TravelerId = service.Beneficiaries.Count == 1 ? service.Beneficiaries.First().OrderTravellerId : (long?)null,
                            SegmentId = service.SoldSegmentId,
                            service.ElectronicTicketId,
                            service.TicketCouponId
                        })
                }),
                Reservations = reservations.Select(reservation => new
                {
                    reservation.Id,
                    reservation.Status,
                    reservation.ExternalReservationRef,
                    reservation.ExpiresAt,
                    Members = reservation.Services.Select(service => new
                    {
                        service.OrderServiceId,
                        service.ObservedStatus,
                        service.ExternalServiceRef
                    })
                }),
                Documents = tickets.Select(ticket => new
                {
                    ticket.Id,
                    ticket.DocumentNumber,
                    ticket.TravelerId,
                    ticket.StatusSummary,
                    ticket.IssuedAt,
                    ticket.IssuedTotal,
                    ticket.CurrencyId,
                    Coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).Select(coupon => new
                    {
                        coupon.Id,
                        coupon.CouponNumber,
                        coupon.OrderServiceId,
                        coupon.JourneySegmentId,
                        coupon.FinancialStatus,
                        coupon.ControlStatus,
                        coupon.IssuanceValue
                    })
                }),
                TimeLimits = order.TimeLimits.Select(limit => new { limit.Type, limit.DueAt, limit.Status }),
                ExternalReferences = order.ExternalReferences.Select(reference => new
                {
                    reference.Type,
                    reference.SourceSystem,
                    reference.Reference
                })
            };

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

        private static object DescribeServiceDetail(Domain.OrderAggregate.Entities.OrderService service)
        {
            if (service.AirTransportDetail is { } air)
                return new { Kind = "AirTransport", air.OrderSegmentId, air.TransitionalFareBasis };

            if (service.SeatDetail is { } seat)
                return new { Kind = "Seat", seat.AssociatedAirOrderServiceId, seat.SoldSeatNumber };

            if (service.BaggageDetail is { } baggage)
                return new { Kind = "Baggage", BaggageKind = baggage.Kind, baggage.Pieces, baggage.Weight, baggage.WeightUnit, baggage.PerPieceWeightLimit };

            if (service.MealDetail is { } meal)
                return new { Kind = "Meal", meal.MealCode, meal.Quantity, meal.SpecialMealCode };

            if (service.LoungeDetail is { } lounge)
                return new { Kind = "Lounge", lounge.AirportId, lounge.LoungeCode, lounge.AccessStart, lounge.AccessEnd, lounge.GuestCount };

            if (service.HotelDetail is { } hotel)
                return new { Kind = "Hotel", hotel.PropertyReference, hotel.CheckIn, hotel.CheckOut, hotel.RoomCount, hotel.GuestCount, hotel.RoomTypeCode };

            if (service.GroundTransportDetail is { } ground)
                return new { Kind = "GroundTransport", ground.PickupLocationReference, ground.DropoffLocationReference, ground.PickupAt, ground.PassengerCount, ground.VehicleTypeCode };

            if (service.GenericDetail is { } generic)
                return new { Kind = "Generic", generic.SchemaName, generic.SchemaVersion };

            return new { Kind = "None" };
        }

    }
}
