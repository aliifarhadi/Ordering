using AeroTech.Framework.Core.Domain.Queries;

namespace AeroTech.Ordering.Query.OrderAggregate.Dto
{
    public sealed class OrderPaginatedRowDto
    {
        public string Id { get; set; } = null!;

        [Grid("PNR")] public string? Pnr { get; set; }

        [Grid("Type")] public string? OrderType { get; set; }

        [Grid("Created")] public string? CreationDate { get; set; }

        [Grid("Channel")] public string? Channel { get; set; }

        [Grid("Agent")] public string? CreatorUser { get; set; }
        public string? CreatorUserEmail { get; set; }

        [Grid("Customer")] public string? Customer { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerType { get; set; }

        [Grid("Passengers")] public int Passengers { get; set; }
        public PassengerSummaryDto PassengerSummary { get; set; } = new();

        [Grid("Status")] public string? Status { get; set; }
        public string? StatusSub { get; set; }

        [Grid("Amount")] public string? GrandTotal { get; set; }

        [Grid("Currency")] public string? Currency { get; set; }

        [Grid("Comm.")] public string? CommissionAmount { get; set; }
        public string? CommissionRate { get; set; }

        [Grid("Ver.")] public int CommercialVersion { get; set; }

        [Grid("TTL")] public DateTimeOffset? TimeToLive { get; set; }
        public RemainingTtlDto? RemainingTtl { get; set; }

        public string UniqueIdentifierId { get; set; } = null!;
        public long? LinkedOrderId { get; set; }
        public string? LinkedPnr { get; set; }
        public string? DownloadUrl { get; set; }
        public FlightSummaryDto FlightSummary { get; set; } = new();
    }

    public sealed class PassengerSummaryDto
    {
        public int Adults { get; set; }
        public int Children { get; set; }
        public int Infants { get; set; }
        public IReadOnlyList<string> Initials { get; set; } = Array.Empty<string>();
        public int Total { get; set; }
    }

    public sealed class RemainingTtlDto
    {
        public int Days { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int TotalMinutes { get; set; }
        public bool IsExpired { get; set; }
    }

    public sealed class FlightSummaryDto
    {
        public string? FlightNumber { get; set; }
        public DateTimeOffset? DepartureDateTime { get; set; }
        public int Stops { get; set; }
        public IReadOnlyList<string> AirportIataCode { get; set; } = Array.Empty<string>();
    }
}
