namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate
{
    public sealed class FulfillmentOptions
    {
        public int ReserveInventoryMaxAttempts { get; set; } = 3;

        public int IssueTicketMaxAttempts { get; set; } = 3;

        public int VoidTicketMaxAttempts { get; set; } = 2;

        public int ReleaseInventoryMaxAttempts { get; set; } = 5;

        public int RetryBaseDelaySeconds { get; set; } = 30;

        public int RetryMaxDelaySeconds { get; set; } = 300;

        public int DefaultHoldMinutes { get; set; } = 30;

        public int PollIntervalSeconds { get; set; } = 10;

        public int PollBatchSize { get; set; } = 20;

        public int LockExpirySeconds { get; set; } = 30;
    }
}
