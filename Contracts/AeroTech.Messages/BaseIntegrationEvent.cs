namespace AeroTech.Messages
{
    public abstract record BaseIntegrationEvent
    {
        public string EventId { get; set; } = default!;

        public string AggregateId { get; set; } = default!;

        public DateTimeOffset TimeOfOccurrence { get; set; }

        public long TenantId { get; set; } = 1;

        public int SchemaVersion { get; set; } = 1;

        public string? CorrelationId { get; set; }

        public string? CausationId { get; set; }

        public string? Actor { get; set; }

        public string SourceSystem { get; set; } = default!;
    }
}
