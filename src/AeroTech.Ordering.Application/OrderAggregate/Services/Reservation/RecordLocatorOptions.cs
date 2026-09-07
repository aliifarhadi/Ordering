namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed class RecordLocatorOptions
    {
        public int MaxAllocationAttempts { get; set; } = 10;
    }
}
