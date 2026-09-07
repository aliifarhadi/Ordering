namespace AeroTech.Ordering.Providers.FlightFlow.Wire
{
    public sealed class FlightFlowEnvelope<T>
    {
        public T? Data { get; set; }

        public FlightFlowError[]? Errors { get; set; }
    }

    public sealed class FlightFlowError
    {
        public int Code { get; set; }

        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
