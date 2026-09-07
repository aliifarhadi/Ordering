namespace AeroTech.Ordering.Application.OrderAggregate.Services.Expiry
{
    public sealed class ExpiryOptions
    {
        public int PollIntervalSeconds { get; set; } = 60;

        public int PollBatchSize { get; set; } = 100;

        public int LockExpirySeconds { get; set; } = 30;
    }
}
