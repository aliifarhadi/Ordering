namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class OrderFlightReadModel
    {
        public long Id { get; set; }

        public long OrderId { get; set; }

        public int Sequence { get; set; }

        public string FlightNumber { get; set; } = default!;

        public int MarketingAirlineId { get; set; }

        public int OriginAirportId { get; set; }

        public int DestinationAirportId { get; set; }

        public DateTimeOffset DepartureDateTime { get; set; }

        public DateTimeOffset ArrivalDateTime { get; set; }
    }
}
