namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed class TicketNumberOptions
    {
        public string Prefix { get; set; } = string.Empty;

        public int SerialLength { get; set; } = 10;

        public int MaxAllocationAttempts { get; set; } = 20;
    }
}
