namespace AeroTech.Ordering.Providers.FlightFlow.Wire
{
    public sealed class FlightSplitSeatsResponse
    {
        public string HoldId { get; set; } = default!;

        public DateTimeOffset ExpiresAt { get; set; }

        public List<FlightSplitSeat> Seats { get; set; } = new();
    }

    public sealed class FlightSplitSeat
    {
        public string SeatHoldReference { get; set; } = default!;
    }
}
