namespace AeroTech.Ordering.Providers.Pricing.Wire
{
    public sealed class PricingEnvelope<T>
    {
        public T? Data { get; set; }

        public PricingError[]? Errors { get; set; }
    }

    public sealed class PricingError
    {
        public int Code { get; set; }

        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
