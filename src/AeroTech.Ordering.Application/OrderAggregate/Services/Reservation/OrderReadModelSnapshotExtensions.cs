using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    internal static class OrderReadModelSnapshotExtensions
    {
        public static OrderReadModelSnapshot ToReadModelSnapshot(this Order order, DateTimeOffset occurredAt)
            => new(
                order.Id,
                order.UniqueIdentifierId,
                order.RecordLocator?.Value,
                order.Status,
                order.Type,
                order.Channel,
                order.CustomerId,
                order.AirlineOfficeId,
                order.CreatorUserId,
                order.CurrencyId,
                order.Pax,
                order.Amount.GrandTotal,
                order.Amount.TaxTotal,
                order.Commission.CommissionAmount,
                order.Commission.CommissionRate,
                order.OrderVersion,
                order.LinkedOrderId,
                order.LinkedPNR,
                order.TimeToLive,
                order.CreationDate,
                occurredAt)
            {
                Travellers = order.Travellers
                    .Select(traveller => new OrderTravellerSnapshot(
                        traveller.Id,
                        traveller.Index,
                        traveller.Name.FirstName,
                        traveller.Name.SurName,
                        traveller.AgeRange))
                    .ToList(),
                Flights = order.Segments
                    .Select(segment => new OrderFlightSnapshot(
                        segment.Id,
                        segment.Sequence,
                        segment.Number,
                        segment.MarketingAirlineId,
                        segment.OriginAirportId,
                        segment.DestinationAirportId,
                        segment.DepartureDateTime,
                        segment.ArrivalDateTime))
                    .ToList()
            };
    }
}
