namespace AeroTech.Ordering.Consumers.Jobs
{
    public sealed class MessageRetentionOptions
    {
        public int PollIntervalMinutes { get; set; } = 60;

        public int OutboxRetentionDays { get; set; } = 7;

        public int InboxRetentionDays { get; set; } = 14;

        public int DeleteBatchSize { get; set; } = 1000;
    }
}
